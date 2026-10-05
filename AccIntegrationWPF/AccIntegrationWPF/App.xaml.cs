using System.IO;
using System.Windows;
using System.Windows.Threading;
using AccIntegration.Core.Models;
using AccIntegration.Infra;
using AccIntegration.Infra.Logging;
using AccIntegration.TerraExplorer;
using AccIntegration.TerraExplorer.Conversion;
using AccIntegration.TerraExplorer.CoordinateSystems;
using AccIntegration.TerraExplorer.Import;
using AccIntegrationWPF.Bridge;
using AccIntegrationWPF.Conversion;
using AccIntegrationWPF.SingleInstance;

namespace AccIntegrationWPF;

/// <summary>
/// Single-instance local processor. The first launch opens the window; later launches
/// (from the CLI) forward --ifc/--output to it and exit. Requests are converted one at a time.
/// Every error is shown to the user in the React UI through the bridge.
/// </summary>
public partial class App : Application
{
    private const string ProductName = "Acc Integration";
    private readonly FileLogger _logger = new(Path.Combine(AppContext.BaseDirectory, "logs"), "WPF");

    private SingleInstanceGuard? _guard;
    private LaunchArgumentsPipe? _pipe;
    private WebViewBridge? _bridge;
    private ConversionQueue? _queue;
    private bool _hasAcceptedConversion;

    protected override void OnStartup(StartupEventArgs e)
    {
        ReportUnhandledErrors();
        _logger.Info("event=wpf.started");
        base.OnStartup(e);

        _guard = new SingleInstanceGuard();
        if (!_guard.IsPrimary)
        {
            // No window in this process: the only way to tell the user is a message box.
            if (!LaunchArgumentsPipe.TrySend(e.Args, _logger))
            {
                LaunchArguments request = LaunchArguments.Parse(e.Args);
                _ = SendCliCompletionAsync(request.CompletionPipeName, new IfcConversionResult
                {
                    Success = false,
                    Status = "failed",
                    Message = "Acc Integration is already open but did not respond. Close it and try again."
                });
                MessageBox.Show(
                    "Acc Integration is already open but did not respond. Close it and try again.",
                    ProductName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown();
            return;
        }

        _bridge = new WebViewBridge(Dispatcher, _logger);

        // SGWorld COM calls start on this UI thread. The placement reader owns a separate
        // STA for its slow native file read so it does not block window input.
        var terraExplorer = new TerraExplorerConnection();
        AccIntegrationContext brain = AccIntegrationFactory.Create(
            terraExplorerImport: new DispatcherTerraExplorerImportService(
                new TerraExplorerImportService(terraExplorer), Dispatcher),
            ifcTo3dmlConverter: new DispatcherIfcTo3dmlConverter(
                new Skyline3dmlConverter(terraExplorer), Dispatcher),
            ifcPlacementReader: new DispatcherIfcPlacementReader(
                new SkylineIfcPlacementReader(terraExplorer), Dispatcher),
            coordinateSystems: new DispatcherCoordinateSystemService(
                new TerraExplorerCoordinateSystemService(terraExplorer), Dispatcher));

        var queue = new ConversionQueue(brain.IfcConversion, _logger);
        _queue = queue;
        var window = new MainWindow(queue, _bridge, brain.CoordinateSystems, _logger);
        MainWindow = window;
        var idleShutdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        idleShutdownTimer.Tick += (_, _) =>
        {
            idleShutdownTimer.Stop();
            if (queue.IsIdle)
                Shutdown();
        };
        queue.QueueChanged += () => Dispatcher.InvokeAsync(() =>
        {
            if (queue.IsIdle && _hasAcceptedConversion)
            {
                window.Hide();
                idleShutdownTimer.Stop();
                idleShutdownTimer.Start();
            }
            else
            {
                idleShutdownTimer.Stop();
            }
        });
        window.Show();

        Accept(e.Args, queue);

        _pipe = new LaunchArgumentsPipe(_logger);
        _pipe.ArgumentsReceived += args => Dispatcher.InvokeAsync(() =>
        {
            Accept(args, queue);
            window.BringToFront();
        });
        _pipe.StartListening();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReportOutstandingJobsAsCancelled();
        _pipe?.Dispose();
        _guard?.Dispose();
        _logger.Info($"event=wpf.exited; exitCode={e.ApplicationExitCode}");
        _logger.Dispose();
        base.OnExit(e);
    }

    private void ReportOutstandingJobsAsCancelled()
    {
        if (_queue is null)
            return;

        var outstanding = new List<LaunchArguments>();
        if (_queue.Current is { } current)
            outstanding.Add(current);
        outstanding.AddRange(_queue.Pending);
        foreach (var job in outstanding)
            _logger.Info($"event=wpf.import.cancelled; status=cancelled; reason=processor_closed; ifcPath={job.IfcPath}; outputPath={job.OutputPath}");

        Task[] reports = outstanding
            .Where(job => !string.IsNullOrWhiteSpace(job.CompletionPipeName))
            .Select(job => IfcConversionCompletionPipe.SendAsync(
                job.CompletionPipeName,
                new IfcConversionResult
                {
                    Success = false,
                    Status = "cancelled",
                    Message = "The local processor closed before the conversion and import completed."
                }))
            .ToArray();

        try
        {
            if (!Task.WhenAll(reports).Wait(TimeSpan.FromSeconds(12)))
                _logger.Error("event=wpf.completion.failed; message=Timed out reporting cancelled jobs during shutdown.");
        }
        catch (AggregateException ex)
        {
            _logger.Error($"event=wpf.completion.failed; exception={ex}");
        }
    }

    private void Accept(string[] args, ConversionQueue queue)
    {
        LaunchArguments request = LaunchArguments.Parse(args);
        if (request.Error is not null)
        {
            _bridge!.SendError(request.Error);
            _ = SendCliCompletionAsync(request.CompletionPipeName, new IfcConversionResult
            {
                Success = false,
                Status = "failed",
                Message = request.Error
            });
            return;
        }

        if (request.HasConversionRequest)
            _hasAcceptedConversion = true;
        queue.Enqueue(request);
    }

    private async Task SendCliCompletionAsync(string? pipeName, IfcConversionResult result)
    {
        if (!string.IsNullOrWhiteSpace(pipeName))
        {
            try
            {
                await IfcConversionCompletionPipe.SendAsync(pipeName, result).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
            {
                _logger.Error($"event=wpf.completion.failed; pipe={pipeName}; exception={ex}");
            }
        }
    }

    private void ReportUnhandledErrors()
    {
        // UI-thread exceptions: show them and keep the window alive.
        DispatcherUnhandledException += (_, args) =>
        {
            if (_bridge is not null)
                _bridge.SendError(args.Exception);
            else
                _logger.Error($"event=wpf.dispatcher.failed; exception={args.Exception}");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (_bridge is not null)
                _bridge.SendError(args.Exception);
            else
                _logger.Error($"event=wpf.task.unobserved; exception={args.Exception}");
            args.SetObserved();
        };

        // Background-thread crash: the process ends, so the message box is the last chance to tell the user.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _logger.Critical($"event=wpf.unhandled; terminating={args.IsTerminating}; exception={args.ExceptionObject}");
            string message = args.ExceptionObject is Exception ex ? ErrorText.From(ex) : "Unknown error.";
            MessageBox.Show(message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}

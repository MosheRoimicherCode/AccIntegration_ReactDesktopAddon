using System.Windows;
using System.Windows.Threading;
using AccIntegration.Infra;
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

    private SingleInstanceGuard? _guard;
    private LaunchArgumentsPipe? _pipe;
    private WebViewBridge? _bridge;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard();
        if (!_guard.IsPrimary)
        {
            // No window in this process: the only way to tell the user is a message box.
            if (!LaunchArgumentsPipe.TrySend(e.Args))
            {
                MessageBox.Show(
                    "Acc Integration is already open but did not respond. Close it and try again.",
                    ProductName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown();
            return;
        }

        _bridge = new WebViewBridge(Dispatcher);
        ReportUnhandledErrors();

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

        var queue = new ConversionQueue(brain.IfcConversion);
        var window = new MainWindow(queue, _bridge, brain.CoordinateSystems);
        MainWindow = window;
        window.Show();

        Accept(e.Args, queue);

        _pipe = new LaunchArgumentsPipe();
        _pipe.ArgumentsReceived += args => Dispatcher.InvokeAsync(() =>
        {
            Accept(args, queue);
            window.BringToFront();
        });
        _pipe.StartListening();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipe?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }

    private void Accept(string[] args, ConversionQueue queue)
    {
        LaunchArguments request = LaunchArguments.Parse(args);
        if (request.Error is not null)
        {
            _bridge!.SendError(request.Error);
            return;
        }

        queue.Enqueue(request);
    }

    private void ReportUnhandledErrors()
    {
        // UI-thread exceptions: show them and keep the window alive.
        DispatcherUnhandledException += (_, args) =>
        {
            _bridge!.SendError(args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _bridge!.SendError(args.Exception);
            args.SetObserved();
        };

        // Background-thread crash: the process ends, so the message box is the last chance to tell the user.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            string message = args.ExceptionObject is Exception ex ? ErrorText.From(ex) : "Unknown error.";
            MessageBox.Show(message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}

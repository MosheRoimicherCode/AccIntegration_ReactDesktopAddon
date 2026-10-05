using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegration.Infra.Logging;
using AccIntegrationWPF.Bridge;
using AccIntegrationWPF.Conversion;
using AccIntegrationWPF.SingleInstance;
using Microsoft.Web.WebView2.Core;

namespace AccIntegrationWPF;

/// <summary>
/// Hosts the React UI and routes the conversion flow through the bridge. No conversion logic here:
/// the brain reads, converts, saves and imports; this window only relays.
///   to React:   conversionRequest → coordinates + coordinateSystemSelected → importProgress → importResult
///   from React: chooseCoordinateSystem, convert, ready, drag, minimize, close
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConversionQueue _queue;
    private readonly WebViewBridge _bridge;
    private readonly ICoordinateSystemService _coordinateSystems;
    private readonly FileLogger _logger;
    private bool _choosingCoordinateSystem;
    private bool _closed;

    public MainWindow(ConversionQueue queue, WebViewBridge bridge, ICoordinateSystemService coordinateSystems, FileLogger logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _coordinateSystems = coordinateSystems ?? throw new ArgumentNullException(nameof(coordinateSystems));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();
        Closed += (_, _) => _closed = true;

        _queue.JobStarted += OnJobStarted;
        _queue.JobReady += OnJobReady;
        _queue.JobProgress += SendProgress;
        _queue.JobCompleted += OnJobCompleted;
        _queue.QueueChanged += SendQueueState;
        _bridge.MessageReceived += OnBridgeMessage;

        InitializeWebView();
    }

    public void BringToFront()
    {
        // A second launch must not reveal the add-on over TerraExplorer's open picker.
        if (_choosingCoordinateSystem || _closed || Dispatcher.HasShutdownStarted)
            return;

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        if (!IsVisible)
            Show();

        // TerraExplorer owns its picker. Raise this window when control returns,
        // then restore its normal z-order instead of leaving it always on top.
        bool wasTopmost = Topmost;
        Topmost = true;
        Topmost = wasTopmost;
        Activate();
        webView.Focus();
    }

    private async void InitializeWebView()
    {
        try
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = true;

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets",
                System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                CoreWebView2HostResourceAccessKind.Allow);

            _bridge.Attach(webView.CoreWebView2);
            webView.CoreWebView2.Navigate("https://appassets/index.html");
        }
        catch (Exception ex)
        {
            // Without the WebView there is no React UI to show the error in.
            _logger.Error($"event=wpf.webview.startup_failed; exception={ex}");
            MessageBox.Show(
                $"The Acc Integration window could not start: {ErrorText.From(ex)}",
                "Acc Integration",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void OnBridgeMessage(string type, JsonElement payload)
    {
        try
        {
            switch (type)
            {
                case "ready":
                    ReplayCurrentJob();
                    SendQueueState();
                    _queue.Start();
                    break;
                case "chooseCoordinateSystem":
                    if (!_choosingCoordinateSystem)
                    {
                        string? currentWkt = IncomingPayloads.ReadCurrentWkt(payload);
                        _choosingCoordinateSystem = true;
                        // A modal dialog must not nest its message loop inside a WebView2 callback.
                        Dispatcher.BeginInvoke(new Action(() => ChooseCoordinateSystem(currentWkt)));
                    }
                    break;
                case "convert":
                    _queue.Submit(IncomingPayloads.ReadConvert(payload));
                    break;
                case "cancelCurrent":
                    _queue.CancelCurrent();
                    break;
                case "switchToNext":
                    _queue.SwitchToNextPending(IncomingPayloads.ReadPlacementDraft(payload));
                    break;
                case "moveQueueItem":
                    if (payload.TryGetProperty("from", out JsonElement from) && from.TryGetInt32(out int fromIndex) &&
                        payload.TryGetProperty("to", out JsonElement to) && to.TryGetInt32(out int toIndex))
                        _queue.MovePending(fromIndex, toIndex);
                    break;
                case "drag":
                case "minimize":
                case "close":
                    HandleWindowCommand(type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _bridge.SendError(ex);
        }
    }

    private void HandleWindowCommand(string command)
    {
        switch (command)
        {
            case "drag":
                // Compatibility for older frontend assets; current pages use app-region: drag.
                // Start the native move loop after the WebView2 callback has returned.
                Dispatcher.BeginInvoke(new Action(() => NativeWindowDrag.Start(this)));
                break;
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "close":
                Application.Current.Shutdown();
                break;
        }
    }

    private void ChooseCoordinateSystem(string? currentWkt)
    {
        try
        {
            Hide();
            CoordinateSystemInfo? chosen = _coordinateSystems.Choose(currentWkt);

            // Cancelled dialog: keep the current selection.
            if (chosen is not null)
                SendCoordinateSystem(chosen);
        }
        catch (Exception ex)
        {
            _bridge.SendError(ex);
        }
        finally
        {
            // Show the same window again after the picker closes, even on cancellation/error.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                _choosingCoordinateSystem = false;
                BringToFront();
            }));
        }
    }

    // After a React reload, bring the page back to where the job is.
    private void ReplayCurrentJob()
    {
        if (_queue.Current is not { } job)
            return;

        OnJobStarted(job);

        if (_queue.Stage == ConversionStage.AwaitingInput && _queue.CurrentPlacement is { } placement)
            OnJobReady(job, placement);
        else if (_queue.Stage == ConversionStage.Converting)
            SendProgress(_queue.LastProgress);
    }

    private void OnJobStarted(LaunchArguments job)
    {
        _bridge.Send("conversionRequest", new { ifcPath = job.IfcPath, outputPath = job.OutputPath });
    }

    private void SendQueueState()
    {
        var current = _queue.Current;
        string? stage = current is null ? null : _queue.Stage switch
        {
            ConversionStage.Preparing => "reading",
            ConversionStage.AwaitingInput => "awaitingInput",
            ConversionStage.Converting => "converting",
            _ => null
        };

        _bridge.Send("queueState", new
        {
            current = current is null ? null : new { ifcPath = current.IfcPath, outputPath = current.OutputPath },
            stage,
            pending = _queue.Pending.Select(job => new { ifcPath = job.IfcPath, outputPath = job.OutputPath }).ToArray()
        });
    }

    private void OnJobReady(LaunchArguments job, IfcPlacement placement)
    {
        _bridge.Send("coordinates", new { x = placement.X, y = placement.Y, z = placement.Z });
        SendCoordinateSystem(placement.CoordinateSystem);

        if (!placement.HasPivot)
            _bridge.SendError("The IFC has no placement. Enter the coordinates before converting.");

        if (placement.CoordinateSystem is null)
            _bridge.SendError("The IFC has no coordinate system. Choose one before converting.");
    }

    private void SendCoordinateSystem(CoordinateSystemInfo? coordinateSystem)
    {
        _bridge.Send(
            "coordinateSystemSelected",
            coordinateSystem is null ? null : new { name = coordinateSystem.Name, wkt = coordinateSystem.Wkt });
    }

    private void SendProgress(double percent)
    {
        _bridge.Send("importProgress", new { percent = (int)Math.Round(percent) });
    }

    private void OnJobCompleted(LaunchArguments job, Exception? failure)
    {
        string status = failure is null ? "uploaded" : failure is OperationCanceledException ? "cancelled" : "failed";
        var result = new IfcConversionResult
        {
            Success = failure is null,
            Status = status,
            Message = failure is null
                ? null
                : failure is OperationCanceledException ? "Cancelled by user." : ErrorText.From(failure)
        };

        _bridge.Send("importResult", new
        {
            success = result.Success,
            status = result.Status,
            ifcPath = job.IfcPath,
            outputPath = job.OutputPath,
            message = result.Message
        });

        if (!string.IsNullOrWhiteSpace(job.CompletionPipeName))
            _ = SendCliCompletionAsync(job.CompletionPipeName, result);
    }

    private async Task SendCliCompletionAsync(string pipeName, IfcConversionResult result)
    {
        try
        {
            await IfcConversionCompletionPipe.SendAsync(pipeName, result).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            _bridge.SendError(ex, $"The command-line caller could not receive the import result: {ex.Message}");
        }
    }
}

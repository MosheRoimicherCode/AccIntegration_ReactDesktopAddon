using System.Text.Json;
using System.Windows.Threading;
using AccIntegration.Infra.Logging;
using Microsoft.Web.WebView2.Core;

namespace AccIntegrationWPF.Bridge;

/// <summary>
/// The only channel between WPF and the React UI. Messages both ways are JSON strings shaped
/// { "type": "...", "payload": ... } (see AccIntegrationReact/src/bridge/types.ts).
/// Created before the WebView exists: messages sent earlier, or before React reports 'ready',
/// are held and delivered in order once React is listening, so no error is lost.
/// </summary>
public sealed class WebViewBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dispatcher _dispatcher;
    private readonly FileLogger _logger;
    private readonly Queue<string> _pending = new();
    private CoreWebView2? _webView;
    private bool _uiReady;

    public WebViewBridge(Dispatcher dispatcher, FileLogger logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Do not use SendError here: it would attempt another write to the failed logger.
        _logger.WriteFailed += message => Send("error", new { message });
        if (_logger.LastWriteFailure is { } failure)
            Send("error", new { message = failure });
    }

    // Raised on the UI thread with the message type and payload (Undefined when absent);
    // malformed messages are ignored.
    public event Action<string, JsonElement>? MessageReceived;

    public void Attach(CoreWebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// Sends a message to React. Safe to call from any thread.
    /// </summary>
    public void Send(string type, object? payload = null)
    {
        string json = JsonSerializer.Serialize(new { type, payload }, JsonOptions);
        _dispatcher.InvokeAsync(() =>
        {
            _pending.Enqueue(json);
            Flush();
        });
    }

    /// <summary>
    /// Shows an error to the user in the React error banner.
    /// </summary>
    public void SendError(string message)
    {
        _logger.Error($"event=wpf.ui.error; message={message}");
        Send("error", new { message });
    }

    public void SendError(Exception exception, string? message = null)
    {
        _logger.Error($"event=wpf.ui.error; message={message ?? exception.Message}; exception={exception}");
        Send("error", new { message = message ?? ErrorText.From(exception) });
    }

    private void Flush()
    {
        if (_webView is null || !_uiReady)
            return;

        // String, not a JSON object: the React bridge JSON.parses event.data.
        while (_pending.TryDequeue(out string? json))
            _webView.PostWebMessageAsString(json);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? type;
        JsonElement payload = default;
        try
        {
            using JsonDocument message = JsonDocument.Parse(e.TryGetWebMessageAsString());
            type = message.RootElement.TryGetProperty("type", out JsonElement typeElement) &&
                   typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;

            // Cloned so it outlives the document.
            if (message.RootElement.TryGetProperty("payload", out JsonElement payloadElement))
                payload = payloadElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.Error($"event=wpf.bridge.invalid_message; exception={ex}");
            return;
        }

        if (type is null)
            return;

        if (type == "ready")
        {
            _uiReady = true;
            Flush();
        }

        MessageReceived?.Invoke(type, payload);
    }
}

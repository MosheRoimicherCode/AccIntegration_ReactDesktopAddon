using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using AccIntegration.Infra.Logging;

namespace AccIntegrationWPF.SingleInstance;

/// <summary>
/// Named pipe between launches: a secondary process sends its command-line arguments (JSON string array)
/// and the primary raises <see cref="ArgumentsReceived"/> for each launch.
/// </summary>
public sealed class LaunchArgumentsPipe : IDisposable
{
    private const string PipeName = "AccIntegrationWPF.Launch";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stop = new();
    private readonly FileLogger _logger;

    public LaunchArgumentsPipe(FileLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Raised on a background thread.
    public event Action<string[]>? ArgumentsReceived;

    public void StartListening()
    {
        _ = Task.Run(ListenAsync);
    }

    /// <summary>
    /// Sends the arguments to the primary instance. Returns false when it could not be reached.
    /// </summary>
    public static bool TrySend(string[] args, FileLogger logger)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect((int)ConnectTimeout.TotalMilliseconds);
            JsonSerializer.Serialize(client, args);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            logger.Error($"event=wpf.launch.forward_failed; exception={ex}");
            return false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);

                string[]? args = await JsonSerializer
                    .DeserializeAsync<string[]>(server, cancellationToken: _stop.Token)
                    .ConfigureAwait(false);

                if (args is not null)
                    ArgumentsReceived?.Invoke(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                _logger.Error($"event=wpf.launch.receive_failed; exception={ex}");
                // A broken or malformed launch message is dropped; keep listening for the next one.
            }
        }
    }
}

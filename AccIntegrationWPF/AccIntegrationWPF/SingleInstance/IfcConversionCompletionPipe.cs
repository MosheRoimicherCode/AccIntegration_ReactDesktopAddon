using System.IO.Pipes;
using System.Text.Json;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.SingleInstance;

internal static class IfcConversionCompletionPipe
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SendAsync(string? pipeName, IfcConversionResult result)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
            return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(pipe, result, JsonOptions, timeout.Token).ConfigureAwait(false);
        await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
    }
}

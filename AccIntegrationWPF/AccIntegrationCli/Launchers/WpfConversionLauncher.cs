using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Launchers;

/// <summary>
/// Starts the WPF processor and waits for its final TerraExplorer import result.
/// </summary>
public sealed class WpfConversionLauncher : IIfcConversionLauncher
{
    public const string WpfExeName = "AccIntegrationWPF.exe";
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _wpfExePath;
    private readonly EventWriter _events;

    public WpfConversionLauncher(EventWriter events, string? wpfExePath = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _wpfExePath = wpfExePath ?? Path.Combine(AppContext.BaseDirectory, WpfExeName);
    }

    public async Task<IfcConversionResult> LaunchAsync(
        string ifcPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Path.GetExtension(ifcPath), ".ifc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only IFC files can be sent to the local processor.", nameof(ifcPath));

        if (!File.Exists(_wpfExePath))
            throw new FileNotFoundException("The local processor (WPF) was not found.", _wpfExePath);

        string pipeName = $"AccIntegrationWPF.Result.{Guid.NewGuid():N}";
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        _events.Info(
            "local_processor.started",
            "Local processor started. Waiting for conversion and TerraExplorer import.",
            new Dictionary<string, object?>
            {
                ["ifcPath"] = ifcPath,
                ["outputPath"] = outputPath
            });

        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = _wpfExePath,
            Arguments = $"--ifc {Quote(ifcPath)} --output {Quote(outputPath)} --completion-pipe {Quote(pipeName)}",
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_wpfExePath) ?? string.Empty
        });

        if (process is null)
            throw new InvalidOperationException($"Failed to start the local processor '{_wpfExePath}'.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CompletionTimeout);
        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
            IfcConversionResult? result = await JsonSerializer
                .DeserializeAsync<IfcConversionResult>(pipe, JsonOptions, timeout.Token)
                .ConfigureAwait(false);

            return result ?? new IfcConversionResult
            {
                Success = false,
                Status = "failed",
                Message = "The local processor returned an empty completion result."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new IfcConversionResult
            {
                Success = false,
                Status = "failed",
                Message = "Timed out waiting for the local processor to finish importing the IFC."
            };
        }
    }

    private static string Quote(string value)
    {
        if (value.Contains('"'))
            throw new ArgumentException($"Path contains a quote character: '{value}'.");

        return $"\"{value}\"";
    }
}

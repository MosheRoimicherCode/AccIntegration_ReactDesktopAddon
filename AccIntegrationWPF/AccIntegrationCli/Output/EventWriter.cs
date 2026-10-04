using System.Text.Json;
using System.Text.Json.Serialization;
using AccIntegration.Core.Models;

namespace AccIntegrationCli.Output;

/// <summary>
/// Writes one JSON event per line: info events to stdout, error events to stderr.
/// Every event is also written to the log file.
/// </summary>
public sealed class EventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;
    private readonly FileLogger _logger;

    public EventWriter(TextWriter standardOutput, TextWriter standardError, FileLogger logger)
    {
        _standardOutput = standardOutput ?? throw new ArgumentNullException(nameof(standardOutput));
        _standardError = standardError ?? throw new ArgumentNullException(nameof(standardError));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Info(string name, string message, Dictionary<string, object?>? data = null)
    {
        Write(name, "info", message, data, _standardOutput);
    }

    public void Error(string name, string message, Dictionary<string, object?>? data = null)
    {
        Write(name, "error", message, data, _standardError);
    }

    private void Write(
        string name,
        string level,
        string message,
        Dictionary<string, object?>? data,
        TextWriter target)
    {
        var uiEvent = new UiWorkflowEvent
        {
            Name = name,
            Level = level,
            Message = message,
            Data = data ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        string json = JsonSerializer.Serialize(uiEvent, JsonOptions);
        string logLine = $"event={name}; message={message}; data={JsonSerializer.Serialize(uiEvent.Data, JsonOptions)}";

        if (level == "error")
            _logger.Error(logLine);
        else
            _logger.Info(logLine);

        target.WriteLine(json);
        target.Flush();
    }
}

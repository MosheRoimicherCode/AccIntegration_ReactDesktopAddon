namespace AccIntegration.Core.Models;

/// <summary>
/// Final result reported by the WPF processor after it imports the converted 3DML.
/// </summary>
public sealed class IfcConversionResult
{
    public bool Success { get; init; }

    public string Status { get; init; } = "failed";

    public string? Message { get; init; }

    // Local launcher diagnostics only; never change the WPF pipe/UI contract.
    [System.Text.Json.Serialization.JsonIgnore]
    public Exception? FailureException { get; init; }
}

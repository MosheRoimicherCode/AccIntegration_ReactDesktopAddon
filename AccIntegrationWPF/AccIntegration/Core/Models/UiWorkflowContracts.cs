namespace AccIntegration.Core.Models;

public sealed class UiWorkflowRequest
{
    public string ProjectUrn { get; set; } = string.Empty;

    public string VersionUrn { get; set; } = string.Empty;

    public string FileUrn { get; set; } = string.Empty;

    public string? DownloadUrl { get; set; }

    public string? LocalFilePath { get; set; }

    public string? PreferredFileName { get; set; }

    public bool ImportIntoTerraExplorer { get; set; } = true;

    public bool ConvertIfcTo3Dml { get; set; } = true;

    public bool ForceRefresh { get; set; }

    public string? IfcTo3DmlToolPath { get; set; }

    public string[]? IfcTo3DmlArgs { get; set; }

    public Dictionary<string, string> Properties { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> ClientData { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class UiWorkflowResult
{
    public bool Success { get; init; }

    public bool UsedCachedFile { get; init; }

    public bool DownloadedFile { get; init; }

    public bool ConvertedIfcTo3Dml { get; init; }

    public bool ImportedIntoTerraExplorer { get; init; }

    public string SourceFilePath { get; init; } = string.Empty;

    public string ImportFilePath { get; init; } = string.Empty;

    public string SourceFileType { get; init; } = string.Empty;

    public string? Converted3dmlPath { get; init; }

    public Dictionary<string, string> Properties { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class UiWorkflowEvent
{
    public string Name { get; init; } = string.Empty;

    public string Level { get; init; } = "info";

    public string Message { get; init; } = string.Empty;

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public Dictionary<string, object?> Data { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class UiWorkflowEventArgs : EventArgs
{
    public UiWorkflowEventArgs(UiWorkflowEvent uiEvent)
    {
        Event = uiEvent ?? throw new ArgumentNullException(nameof(uiEvent));
    }

    public UiWorkflowEvent Event { get; }
}

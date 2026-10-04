namespace AccIntegration.Core.Models;

public sealed class LocalFileResult
{
    public string FilePath { get; init; } = string.Empty;

    public DownloadedFileMetadata Metadata { get; init; } = new();

    // True when the file was already on disk and no download happened.
    public bool UsedLocalFile { get; init; }

    public bool Downloaded { get; init; }

    public bool Is3dml { get; init; }
}

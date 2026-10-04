namespace AccIntegration.Core.Models;

public sealed class DownloadedFileMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Required metadata for ACC/APS identity.
    public string ProjectUrn { get; set; } = string.Empty;

    public string VersionUrn { get; set; } = string.Empty;

    public string FileUrn { get; set; } = string.Empty;

    // Name used for the stored local file.
    public string FileName { get; set; } = string.Empty;

    // Remote URL the file was downloaded from.
    public string SourceReference { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string TypeFolderName { get; set; } = "OTHER";

    public string LocalPath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime DownloadedAtUtc { get; set; } = DateTime.UtcNow;

    public Dictionary<string, string> Properties { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

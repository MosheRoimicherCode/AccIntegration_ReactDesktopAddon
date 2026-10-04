namespace AccIntegration.Core.Models;

public sealed class FileOperationResult
{
    // The operation that was run on the file.
    public FileOperation Operation { get; init; }

    public ImportMode ImportMode { get; init; }

    // The local file the operation ran on, and whether it was reused or downloaded.
    public LocalFileResult File { get; init; } = new();

    // Where the local processor will write the 3DML (ConvertIfcTo3dml only).
    public string? OutputPath { get; init; }
}

using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Gets a file into managed storage and runs the operation mapped to its extension and the UI's import mode.
/// </summary>
public interface IFileOrchestrator
{
    /// <summary>
    /// Reuses the local copy for the project/version, or downloads it.
    /// </summary>
    /// <param name="fileName">Name to store under; when null the server's name is used.</param>
    Task<FileOperationResult> RunAsync(
        string fileUrl,
        string? fileName,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        ImportMode importMode = ImportMode.Default,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an existing local file under the URNs, then runs its operation.
    /// </summary>
    Task<FileOperationResult> RunLocalFileAsync(
        string sourcePath,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        ImportMode importMode = ImportMode.Default,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);
}

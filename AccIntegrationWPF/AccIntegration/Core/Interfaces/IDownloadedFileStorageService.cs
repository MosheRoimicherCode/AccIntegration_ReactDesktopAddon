using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Gets files into managed storage with their metadata.
/// </summary>
public interface IDownloadedFileStorageService
{
    /// <summary>
    /// Returns the local copy when metadata and the file on disk both exist (3DML preferred),
    /// otherwise downloads the file to its folder and writes its metadata with the caller's properties.
    /// </summary>
    /// <param name="fileName">Name to store under; when null the server's name is used.</param>
    Task<LocalFileResult> GetOrDownloadAsync(
        string fileUrl,
        string? fileName,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an existing local file (copied into its type folder) with metadata under the given URNs.
    /// </summary>
    Task<LocalFileResult> StoreLocalFileAsync(
        string sourcePath,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);
}

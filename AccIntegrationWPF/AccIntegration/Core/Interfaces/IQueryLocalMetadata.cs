using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Reads downloaded file metadata records from local storage.
/// </summary>
public interface IQueryLocalMetadata
{
    Task<IReadOnlyList<DownloadedFileMetadata>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<DownloadedFileMetadata?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DownloadedFileMetadata?> GetByProjectAndVersionAsync(
        string projectUrn,
        string versionUrn,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DownloadedFileMetadata>> GetAllByProjectAndVersionAsync(
        string projectUrn,
        string versionUrn,
        CancellationToken cancellationToken = default);
}

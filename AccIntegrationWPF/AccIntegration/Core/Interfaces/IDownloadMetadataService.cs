using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Writes downloaded file metadata records to local storage.
/// </summary>
public interface IDownloadMetadataService
{
    Task<DownloadedFileMetadata> CreateAsync(
        DownloadedFileMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        DownloadedFileMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

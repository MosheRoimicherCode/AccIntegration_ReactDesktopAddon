using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class DownloadMetadataService : IDownloadMetadataService
{
    private readonly MetadataShardStore _store;

    public DownloadMetadataService(IDownloadStoragePathProvider pathProvider)
    {
        _store = new MetadataShardStore(pathProvider);
    }

    public async Task<DownloadedFileMetadata> CreateAsync(
        DownloadedFileMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        Validate(metadata);

        await _store.AppendAsync(metadata, cancellationToken).ConfigureAwait(false);
        return metadata;
    }

    public Task<bool> UpdateAsync(
        DownloadedFileMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        Validate(metadata);

        return _store.UpdateAsync(metadata, cancellationToken);
    }

    /// <summary>
    /// Deletes the metadata record only; the physical file is not touched.
    /// </summary>
    public Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _store.DeleteAsync(id, cancellationToken);
    }

    private static void Validate(DownloadedFileMetadata metadata)
    {
        if (metadata.Id == Guid.Empty)
            throw new ArgumentException("Id is required.", nameof(metadata));

        if (string.IsNullOrWhiteSpace(metadata.ProjectUrn))
            throw new ArgumentException("ProjectUrn is required.", nameof(metadata));

        if (string.IsNullOrWhiteSpace(metadata.VersionUrn))
            throw new ArgumentException("VersionUrn is required.", nameof(metadata));

        if (string.IsNullOrWhiteSpace(metadata.FileUrn))
            throw new ArgumentException("FileUrn is required.", nameof(metadata));

        if (string.IsNullOrWhiteSpace(metadata.FileName))
            throw new ArgumentException("FileName is required.", nameof(metadata));
    }
}

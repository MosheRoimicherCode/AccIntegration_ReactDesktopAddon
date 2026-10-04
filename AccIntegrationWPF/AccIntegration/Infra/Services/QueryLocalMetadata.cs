using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class QueryLocalMetadata : IQueryLocalMetadata
{
    private readonly MetadataShardStore _store;

    public QueryLocalMetadata(IDownloadStoragePathProvider pathProvider)
    {
        _store = new MetadataShardStore(pathProvider);
    }

    /// <summary>
    /// Returns all records, newest first.
    /// </summary>
    public async Task<IReadOnlyList<DownloadedFileMetadata>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        List<DownloadedFileMetadata> records = await _store.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        records.Reverse();
        return records;
    }

    public async Task<DownloadedFileMetadata?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DownloadedFileMetadata> records = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return records.FirstOrDefault(record => record.Id == id);
    }

    /// <summary>
    /// Returns the newest record matching the project and version.
    /// </summary>
    public async Task<DownloadedFileMetadata?> GetByProjectAndVersionAsync(
        string projectUrn,
        string versionUrn,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DownloadedFileMetadata> records = await GetAllByProjectAndVersionAsync(
            projectUrn,
            versionUrn,
            cancellationToken).ConfigureAwait(false);

        return records.FirstOrDefault();
    }

    /// <summary>
    /// Returns every record matching the project and version, newest first.
    /// </summary>
    public async Task<IReadOnlyList<DownloadedFileMetadata>> GetAllByProjectAndVersionAsync(
        string projectUrn,
        string versionUrn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectUrn))
            throw new ArgumentException("ProjectUrn is required.", nameof(projectUrn));

        if (string.IsNullOrWhiteSpace(versionUrn))
            throw new ArgumentException("VersionUrn is required.", nameof(versionUrn));

        projectUrn = projectUrn.Trim();
        versionUrn = versionUrn.Trim();

        IReadOnlyList<DownloadedFileMetadata> records = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return records
            .Where(record =>
                string.Equals(record.ProjectUrn, projectUrn, StringComparison.Ordinal) &&
                string.Equals(record.VersionUrn, versionUrn, StringComparison.Ordinal))
            .ToList();
    }
}

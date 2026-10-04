using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Reports whether a metadata record exists for a project/version AND its file is on disk.
/// </summary>
public sealed class ExistsCommand : ICliCommand
{
    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly IQueryLocalFiles _localFiles;
    private readonly EventWriter _events;

    public ExistsCommand(IQueryLocalMetadata metadataQuery, IQueryLocalFiles localFiles, EventWriter events)
    {
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _localFiles = localFiles ?? throw new ArgumentNullException(nameof(localFiles));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public string Name => "exists";

    public string Usage => "exists --project-urn <URN> --version-urn <URN>";

    public bool RequiresLicense => true;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        string projectUrn = args.GetRequired("project-urn");
        string versionUrn = args.GetRequired("version-urn");

        IReadOnlyList<DownloadedFileMetadata> records = await _metadataQuery
            .GetAllByProjectAndVersionAsync(projectUrn, versionUrn, cancellationToken)
            .ConfigureAwait(false);

        DownloadedFileMetadata? existing = records.FirstOrDefault(_localFiles.Exists);

        _events.Info(
            "storage.exists.completed",
            existing is null ? "No local file found for project/version." : "Matching file exists locally.",
            new Dictionary<string, object?>
            {
                ["exists"] = existing is not null,
                ["projectUrn"] = projectUrn,
                ["versionUrn"] = versionUrn,
                ["localPath"] = existing?.LocalPath
            });
        return 0;
    }
}

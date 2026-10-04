using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Returns the newest metadata record for a project/version.
/// </summary>
public sealed class GetCommand : ICliCommand
{
    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly EventWriter _events;

    public GetCommand(IQueryLocalMetadata metadataQuery, EventWriter events)
    {
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public string Name => "get";

    public string Usage => "get --project-urn <URN> --version-urn <URN>";

    public bool RequiresLicense => true;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        string projectUrn = args.GetRequired("project-urn");
        string versionUrn = args.GetRequired("version-urn");

        DownloadedFileMetadata? metadata = await _metadataQuery
            .GetByProjectAndVersionAsync(projectUrn, versionUrn, cancellationToken)
            .ConfigureAwait(false);

        _events.Info(
            "metadata.get.completed",
            metadata is null ? "No metadata found for project/version." : "Metadata found.",
            new Dictionary<string, object?>
            {
                ["found"] = metadata is not null,
                ["projectUrn"] = projectUrn,
                ["versionUrn"] = versionUrn,
                // Top level too: the ACC web UI reads data.localPath.
                ["localPath"] = metadata?.LocalPath,
                ["metadata"] = metadata
            });
        return 0;
    }
}

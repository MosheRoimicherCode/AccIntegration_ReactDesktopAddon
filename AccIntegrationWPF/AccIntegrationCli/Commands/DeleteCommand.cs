using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Deletes the newest metadata record for a project/version, and the file on disk with --delete-file true.
/// </summary>
public sealed class DeleteCommand : ICliCommand
{
    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly IDownloadMetadataService _metadataService;
    private readonly ILocalFileActions _fileActions;
    private readonly EventWriter _events;

    public DeleteCommand(
        IQueryLocalMetadata metadataQuery,
        IDownloadMetadataService metadataService,
        ILocalFileActions fileActions,
        EventWriter events)
    {
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _fileActions = fileActions ?? throw new ArgumentNullException(nameof(fileActions));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public string Name => "delete";

    public string Usage => "delete --project-urn <URN> --version-urn <URN> [--delete-file true|false]";

    public bool RequiresLicense => true;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        string projectUrn = args.GetRequired("project-urn");
        string versionUrn = args.GetRequired("version-urn");
        bool deletePhysicalFile = args.GetBool(false, "delete-file", "delete-physical-file");

        DownloadedFileMetadata? metadata = await _metadataQuery
            .GetByProjectAndVersionAsync(projectUrn, versionUrn, cancellationToken)
            .ConfigureAwait(false);

        bool fileDeleted = metadata is not null && deletePhysicalFile && _fileActions.Delete(metadata.LocalPath);

        bool deleted = metadata is not null &&
                       await _metadataService.DeleteAsync(metadata.Id, cancellationToken).ConfigureAwait(false);

        _events.Info(
            "metadata.delete.completed",
            deleted ? "Metadata record deleted." : "No metadata found for project/version.",
            new Dictionary<string, object?>
            {
                ["deleted"] = deleted,
                ["fileDeleted"] = fileDeleted,
                ["projectUrn"] = projectUrn,
                ["versionUrn"] = versionUrn,
                ["id"] = metadata?.Id,
                ["localPath"] = metadata?.LocalPath
            });
        return 0;
    }
}

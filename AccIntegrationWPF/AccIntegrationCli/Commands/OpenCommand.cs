using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Shows a file in Windows Explorer: the given --path, or the newest stored IFC for the project/version (as in TEZ).
/// </summary>
public sealed class OpenCommand : ICliCommand
{
    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly IQueryLocalFiles _localFiles;
    private readonly ILocalFileActions _fileActions;
    private readonly EventWriter _events;

    public OpenCommand(
        IQueryLocalMetadata metadataQuery,
        IQueryLocalFiles localFiles,
        ILocalFileActions fileActions,
        EventWriter events)
    {
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _localFiles = localFiles ?? throw new ArgumentNullException(nameof(localFiles));
        _fileActions = fileActions ?? throw new ArgumentNullException(nameof(fileActions));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public string Name => "open";

    public string Usage => "open --path <FILE> | open --project-urn <URN> --version-urn <URN>";

    // Local-only action; TEZ allowed it without a license.
    public bool RequiresLicense => false;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        string? path = args.GetOptional("path");
        string? projectUrn = null;
        string? versionUrn = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            projectUrn = args.GetRequired("project-urn");
            versionUrn = args.GetRequired("version-urn");
            path = await FindStoredIfcAsync(projectUrn, versionUrn, cancellationToken).ConfigureAwait(false);
        }

        _events.Info(
            "storage.open.started",
            "Opening file location.",
            new Dictionary<string, object?> { ["path"] = path });

        bool launched = _fileActions.ShowInFolder(path);

        _events.Info(
            "storage.open.completed",
            "File location open request was sent.",
            new Dictionary<string, object?>
            {
                ["path"] = path,
                ["projectUrn"] = projectUrn,
                ["versionUrn"] = versionUrn,
                ["launched"] = launched
            });
        return 0;
    }

    private async Task<string> FindStoredIfcAsync(string projectUrn, string versionUrn, CancellationToken cancellationToken)
    {
        IReadOnlyList<DownloadedFileMetadata> records = await _metadataQuery
            .GetAllByProjectAndVersionAsync(projectUrn, versionUrn, cancellationToken)
            .ConfigureAwait(false);

        DownloadedFileMetadata? ifc = records.FirstOrDefault(record =>
            string.Equals(Path.GetExtension(record.LocalPath), ".ifc", StringComparison.OrdinalIgnoreCase) &&
            _localFiles.Exists(record));

        return ifc?.LocalPath
            ?? throw new FileNotFoundException("No stored IFC file was found for the provided project/version URNs.");
    }
}

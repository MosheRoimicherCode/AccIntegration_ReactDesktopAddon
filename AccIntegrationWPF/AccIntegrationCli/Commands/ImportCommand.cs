using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Stores an existing local file under the URNs, then runs the operation mapped to its type.
/// </summary>
public sealed class ImportCommand : ICliCommand
{
    private readonly IFileOrchestrator _files;
    private readonly WorkflowReporter _reporter;

    public ImportCommand(IFileOrchestrator files, WorkflowReporter reporter)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public string Name => "import";

    public string Usage =>
        "import --path <LOCAL_FILE> --project-urn <URN> --version-urn <URN> --file-urn <URN> [--hub-urn <URN>] " +
        "[--import-mode image_layer|elevation_layer] [--properties \"{\\\"Key\\\":\\\"Value\\\"}\"]";

    public bool RequiresLicense => true;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        string path = args.GetRequired("path", "local-file-path");
        string projectUrn = args.GetRequired("project-urn");
        string versionUrn = args.GetRequired("version-urn");
        string fileUrn = args.GetRequired("file-urn");

        Dictionary<string, string> properties = WorkflowInput.ReadProperties(args);
        ImportMode importMode = WorkflowInput.ReadImportMode(args, properties);

        _reporter.Started(new Dictionary<string, object?>
        {
            ["path"] = path,
            ["projectUrn"] = projectUrn,
            ["versionUrn"] = versionUrn,
            ["fileUrn"] = fileUrn,
            ["importMode"] = importMode
        });

        try
        {
            FileOperationResult result = await _files
                .RunLocalFileAsync(path, projectUrn, versionUrn, fileUrn, importMode, properties, cancellationToken)
                .ConfigureAwait(false);

            return _reporter.Completed(result) ? 0 : 1;
        }
        catch (Exception ex)
        {
            string failedPath = ex is LayerImportException importFailure ? importFailure.FilePath : path;
            Exception reportedException = ex is LayerImportException { InnerException: not null } layerFailure
                ? layerFailure.InnerException!
                : ex;

            if (FileOperationMap.Resolve(failedPath) is FileOperation.ImportToTerraExplorer or FileOperation.ConvertIfcTo3dml)
            {
                _reporter.FailedLayerImport(failedPath, reportedException);
                return 1;
            }

            throw;
        }
    }
}

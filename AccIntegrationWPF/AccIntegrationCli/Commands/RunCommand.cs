using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Main flow: reuse the local file or download it, then run the operation mapped to its type.
/// Registered as 'run' and as 'download' (the name the ACC web UI uses, from TEZ).
/// </summary>
public sealed class RunCommand : ICliCommand
{
    private readonly IFileOrchestrator _files;
    private readonly WorkflowReporter _reporter;

    public RunCommand(string name, IFileOrchestrator files, WorkflowReporter reporter)
    {
        Name = name;
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public string Name { get; }

    public string Usage =>
        $"{Name} --url <URL> --project-urn <URN> --version-urn <URN> --file-urn <URN> [--hub-urn <URN>] " +
        "[--file-name|--preferred-file-name <NAME.EXT>] [--import-mode image_layer|elevation_layer] " +
        "[--properties \"{\\\"Key\\\":\\\"Value\\\"}\"]";

    public bool RequiresLicense => true;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        string url = args.GetRequired("url", "file-url");
        string projectUrn = args.GetRequired("project-urn");
        string versionUrn = args.GetRequired("version-urn");
        string fileUrn = args.GetRequired("file-urn");

        // Optional: the web UI omits it for RVT (ACC serves a converted IFC); the server's name is used then.
        string? fileName = args.GetOptional("file-name", "preferred-file-name");

        Dictionary<string, string> properties = WorkflowInput.ReadProperties(args);
        ImportMode importMode = WorkflowInput.ReadImportMode(args, properties);

        _reporter.Started(new Dictionary<string, object?>
        {
            ["url"] = url,
            ["projectUrn"] = projectUrn,
            ["versionUrn"] = versionUrn,
            ["fileUrn"] = fileUrn,
            ["fileName"] = fileName,
            ["importMode"] = importMode
        });

        FileOperationResult result = await _files
            .RunAsync(url, fileName, projectUrn, versionUrn, fileUrn, importMode, properties, cancellationToken)
            .ConfigureAwait(false);

        _reporter.Completed(result);
        return 0;
    }
}

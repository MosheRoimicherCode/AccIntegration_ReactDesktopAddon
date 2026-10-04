using AccIntegration.Infra;
using AccIntegration.TerraExplorer;
using AccIntegration.TerraExplorer.Import;
using AccIntegration.TerraExplorer.License;
using AccIntegrationCli;
using AccIntegrationCli.Commands;
using AccIntegrationCli.Launchers;
using AccIntegrationCli.Output;

// Keep the real stdout for JSON events; anything else written to Console goes to stderr.
TextWriter standardOutput = Console.Out;
Console.SetOut(Console.Error);

using var logger = new FileLogger(Path.Combine(AppContext.BaseDirectory, "logs"));
logger.LogUnhandledExceptions();

var events = new EventWriter(standardOutput, Console.Error, logger);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

// Attaches to TerraExplorer lazily, on the first license check or import.
var terraExplorer = new TerraExplorerConnection();
var licenseService = new TerraExplorerAccLicenseService(terraExplorer);
AccIntegrationContext brain = AccIntegrationFactory.Create(
    new TerraExplorerImportService(terraExplorer),
    new WpfConversionLauncher());

var workflow = new WorkflowReporter(events);

// Command names and options match what the ACC web UI sends (TEZ's DesktopTEZ.exe contract).
ICliCommand[] commands =
{
    new RunCommand("download", brain.Files, workflow),
    new RunCommand("run", brain.Files, workflow),
    new ImportCommand(brain.Files, workflow),
    new GetCommand(brain.MetadataQuery, events),
    new ListCommand(brain.MetadataQuery, events),
    new DeleteCommand(brain.MetadataQuery, brain.MetadataService, brain.FileActions, events),
    new ExistsCommand(brain.MetadataQuery, brain.LocalFiles, events),
    new OpenCommand(brain.MetadataQuery, brain.LocalFiles, brain.FileActions, events),
    new AccExtensionCommand(licenseService, events)
};

var application = new CliApplication(commands, licenseService, events, logger, Console.Error);

return await application.RunAsync(args, cancellation.Token);

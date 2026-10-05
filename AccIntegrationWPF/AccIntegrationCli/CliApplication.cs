using AccIntegration.Core.Interfaces;
using AccIntegration.Infra.Logging;
using AccIntegrationCli.Commands;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli;

/// <summary>
/// Parses the command line, runs the matching command and turns the outcome into an exit code.
/// </summary>
public sealed class CliApplication
{
    private readonly Dictionary<string, ICliCommand> _commands;
    private readonly IAccLicenseService _licenseService;
    private readonly EventWriter _events;
    private readonly FileLogger _logger;
    private readonly TextWriter _helpOutput;

    public CliApplication(
        IEnumerable<ICliCommand> commands,
        IAccLicenseService licenseService,
        EventWriter events,
        FileLogger logger,
        TextWriter helpOutput)
    {
        _commands = commands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _helpOutput = helpOutput ?? throw new ArgumentNullException(nameof(helpOutput));
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        _logger.Info($"Command activation: {CliArguments.FormatForLog(args)}");

        string commandName = args.Length > 0 ? args[0].Trim() : string.Empty;

        try
        {
            CliArguments parsed = CliArguments.Parse(args);

            if (parsed.Command is "" or "help" or "--help" or "-h")
            {
                PrintUsage();
                return 0;
            }

            if (!_commands.TryGetValue(parsed.Command, out ICliCommand? command))
            {
                _events.Error(
                    "command.unknown",
                    $"Unknown command: {commandName}",
                    new Dictionary<string, object?> { ["command"] = commandName });
                return 1;
            }

            if (command.RequiresLicense && !IsLicensed(parsed.Command))
                return 1;

            int exitCode = await command.ExecuteAsync(parsed, cancellationToken).ConfigureAwait(false);
            _logger.Info($"Command '{parsed.Command}' completed with exit code {exitCode}.");
            return exitCode;
        }
        catch (Exception ex)
        {
            _events.Error(
                "command.failed",
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["command"] = commandName,
                    ["exceptionType"] = ex.GetType().FullName
                }, ex);
            return 1;
        }
    }

    private bool IsLicensed(string commandName)
    {
        _events.Info(
            "license.validation.started",
            "Validating TerraExplorer Extensions module license.",
            new Dictionary<string, object?> { ["operation"] = commandName });

        try
        {
            _licenseService.EnsureLicensed();
        }
        catch (Exception ex)
        {
            _events.Error(
                "license.validation.failed",
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["operation"] = commandName,
                    ["exceptionType"] = ex.GetType().FullName
                }, ex);
            return false;
        }

        _events.Info(
            "license.validation.completed",
            "ACC extension license is valid.",
            new Dictionary<string, object?> { ["operation"] = commandName });
        return true;
    }

    private void PrintUsage()
    {
        string exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "DesktopTEZ";

        _helpOutput.WriteLine("Usage:");
        foreach (ICliCommand command in _commands.Values)
        {
            _helpOutput.WriteLine($"  {exeName} {command.Usage}");
        }

        _helpOutput.WriteLine($"  {exeName} help");
        _helpOutput.WriteLine();
        _helpOutput.WriteLine("Events are written as one JSON object per line: info on stdout, errors on stderr.");
        _helpOutput.WriteLine("Exit code: 0 on success, 1 on failure.");
    }
}

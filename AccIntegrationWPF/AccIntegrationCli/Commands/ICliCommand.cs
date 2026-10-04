using AccIntegrationCli.Input;

namespace AccIntegrationCli.Commands;

public interface ICliCommand
{
    // Lower-case name typed as the first argument.
    string Name { get; }

    // One usage line for help output.
    string Usage { get; }

    // False for commands that must work without a valid ACC license (status and local-only actions).
    bool RequiresLicense { get; }

    /// <returns>The process exit code.</returns>
    Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken);
}

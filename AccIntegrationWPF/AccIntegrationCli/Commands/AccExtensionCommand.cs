using AccIntegration.Core.Interfaces;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Reports whether the ACC extension is licensed (event license.extension.checked, data.exists), as TEZ did.
/// Exit code 1 when it is not.
/// </summary>
public sealed class AccExtensionCommand : ICliCommand
{
    private readonly IAccLicenseService _licenseService;
    private readonly EventWriter _events;

    public AccExtensionCommand(IAccLicenseService licenseService, EventWriter events)
    {
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public string Name => "acc-extension";

    public string Usage => "acc-extension";

    // This command is the license check itself.
    public bool RequiresLicense => false;

    public Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        args.EnsureNoOptions();

        bool exists;
        string details;
        Exception? failure = null;
        try
        {
            _licenseService.EnsureLicensed();
            exists = true;
            details = string.Empty;
        }
        catch (Exception ex)
        {
            failure = ex;
            exists = false;
            details = ex.Message;
        }

        _events.Info(
            "license.extension.checked",
            exists ? "ACC extension exists." : "ACC extension does not exist.",
            new Dictionary<string, object?>
            {
                ["exists"] = exists,
                ["details"] = details
            }, failure);

        return Task.FromResult(exists ? 0 : 1);
    }
}

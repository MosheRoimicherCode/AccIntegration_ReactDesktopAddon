using AccIntegration.Core.Interfaces;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.License;

/// <summary>
/// The ACC extension is licensed when the TerraExplorer 'Extensions' license module
/// exists, is valid, and lists ACC among its extensions.
/// </summary>
public sealed class TerraExplorerAccLicenseService : IAccLicenseService
{
    private const string ModuleName = "Extensions";
    private const string AccExtensionName = "ACC";
    private static readonly char[] ExtensionSeparators = { ',', ';', '|', ' ', '\t', '\r', '\n' };

    private readonly TerraExplorerConnection _connection;

    public TerraExplorerAccLicenseService(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public void EnsureLicensed()
    {
        IModuleLicenseInfo81? info;
        try
        {
            info = _connection.World.LicenseManager.GetModuleInfo(ModuleName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not read the TerraExplorer license: {ex.Message}", ex);
        }

        if (info is null || !info.Exist)
            throw new InvalidOperationException($"The TerraExplorer '{ModuleName}' license module does not exist.");

        if (!info.Valid)
        {
            throw new InvalidOperationException(
                $"The TerraExplorer '{ModuleName}' license module is not valid: {info.ErrString} (code {info.ErrCode}).");
        }

        if (!HasAccExtension(info.Extensions))
        {
            throw new InvalidOperationException(
                $"ACC extension is missing in the TerraExplorer '{ModuleName}' license module. " +
                $"Licensed extensions: '{info.Extensions}'.");
        }
    }

    // Matches ACC as a whole entry so values such as "ACCESS" do not count.
    private static bool HasAccExtension(string? extensions)
    {
        if (string.IsNullOrWhiteSpace(extensions))
            return false;

        return extensions
            .Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Any(entry => string.Equals(entry.Trim(), AccExtensionName, StringComparison.OrdinalIgnoreCase));
    }
}

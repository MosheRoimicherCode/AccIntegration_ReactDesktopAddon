namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Checks the local ACC extension subscription.
/// </summary>
public interface IAccLicenseService
{
    /// <summary>
    /// Throws when the ACC extension is missing or invalid in the local license.
    /// </summary>
    void EnsureLicensed();
}

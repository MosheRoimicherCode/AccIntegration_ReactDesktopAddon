using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Hands an IFC over to the local processor (WPF UI) that converts it to 3DML.
/// Completes when the WPF processor reports the final TerraExplorer import result.
/// </summary>
public interface IIfcConversionLauncher
{
    Task<IfcConversionResult> LaunchAsync(
        string ifcPath,
        string outputPath,
        CancellationToken cancellationToken = default);
}

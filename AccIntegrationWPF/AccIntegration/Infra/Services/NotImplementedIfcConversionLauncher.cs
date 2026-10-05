using AccIntegration.Core.Interfaces;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Used when no conversion launcher is supplied to the factory (e.g. tests).
/// </summary>
public sealed class NotImplementedIfcConversionLauncher : IIfcConversionLauncher
{
    public Task<AccIntegration.Core.Models.IfcConversionResult> LaunchAsync(
        string ifcPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromException<AccIntegration.Core.Models.IfcConversionResult>(
            new NotSupportedException(
                $"IFC to 3DML conversion is not available. The file is stored at '{ifcPath}'."));
    }
}

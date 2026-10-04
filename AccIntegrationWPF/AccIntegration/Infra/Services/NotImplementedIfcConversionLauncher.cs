using AccIntegration.Core.Interfaces;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Used when no conversion launcher is supplied to the factory (e.g. tests).
/// </summary>
public sealed class NotImplementedIfcConversionLauncher : IIfcConversionLauncher
{
    public void Launch(string ifcPath, string outputPath)
    {
        throw new NotSupportedException(
            $"IFC to 3DML conversion is not available. The file is stored at '{ifcPath}'.");
    }
}

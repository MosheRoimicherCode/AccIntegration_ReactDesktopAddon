using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Used when no converter is supplied to the factory (e.g. the CLI, which only hands IFCs over).
/// </summary>
public sealed class NotImplementedIfcTo3dmlConverter : IIfcTo3dmlConverter
{
    public Task ConvertAsync(
        string ifcPath,
        string outputPath,
        IfcPlacement placement,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("IFC to 3DML conversion is only available in the local processor.");
    }
}

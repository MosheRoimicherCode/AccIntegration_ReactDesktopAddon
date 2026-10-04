using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Converts an IFC file into a 3DML file at the given placement.
/// </summary>
public interface IIfcTo3dmlConverter
{
    /// <param name="placement">Pivot and coordinate system to use (required).</param>
    /// <param name="progress">Conversion progress, 0-100.</param>
    Task ConvertAsync(
        string ifcPath,
        string outputPath,
        IfcPlacement placement,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

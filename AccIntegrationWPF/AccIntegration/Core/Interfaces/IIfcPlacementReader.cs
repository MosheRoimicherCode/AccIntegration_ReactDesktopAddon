using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Reads the pivot and coordinate system stored in an IFC file.
/// The coordinate system name may be empty; describing it is <see cref="ICoordinateSystemService"/>'s job.
/// </summary>
public interface IIfcPlacementReader
{
    Task<IfcPlacement> ReadAsync(string ifcPath, CancellationToken cancellationToken = default);
}

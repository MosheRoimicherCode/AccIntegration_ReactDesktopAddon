using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

public interface ICoordinateSystemService
{
    /// <summary>
    /// Returns the coordinate system with a readable name for the WKT.
    /// </summary>
    CoordinateSystemInfo Describe(string wkt);

    /// <summary>
    /// Lets the user pick a coordinate system, starting from the current one. Null when cancelled.
    /// </summary>
    CoordinateSystemInfo? Choose(string? currentWkt);
}

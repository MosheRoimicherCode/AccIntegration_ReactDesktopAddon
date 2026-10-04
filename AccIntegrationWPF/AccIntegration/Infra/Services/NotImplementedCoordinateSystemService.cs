using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Used when no coordinate system service is supplied to the factory (e.g. the CLI).
/// </summary>
public sealed class NotImplementedCoordinateSystemService : ICoordinateSystemService
{
    public CoordinateSystemInfo Describe(string wkt)
    {
        throw new NotSupportedException("Coordinate systems are only available in the local processor.");
    }

    public CoordinateSystemInfo? Choose(string? currentWkt)
    {
        throw new NotSupportedException("Coordinate systems are only available in the local processor.");
    }
}

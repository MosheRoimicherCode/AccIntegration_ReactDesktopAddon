using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.CoordinateSystems;

/// <summary>
/// Coordinate systems through TerraExplorer's CoordServices: its own picker dialog and WKT descriptions.
/// </summary>
public sealed class TerraExplorerCoordinateSystemService : ICoordinateSystemService
{
    private const string DialogTitle = "Choose the IFC coordinate system";

    private readonly TerraExplorerConnection _connection;

    public TerraExplorerCoordinateSystemService(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public CoordinateSystemInfo Describe(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT is required.", nameof(wkt));

        ICoordinateSystem81 coordinateSystem = _connection.CoordServices.CreateCoordinateSystem(wkt);
        string? description = coordinateSystem?.WktDescription;

        return new CoordinateSystemInfo
        {
            Wkt = wkt,
            Name = string.IsNullOrWhiteSpace(description) ? "Custom coordinate system" : description
        };
    }

    public CoordinateSystemInfo? Choose(string? currentWkt)
    {
        // Modal TerraExplorer dialog; returns an empty string when the user cancels.
        string chosenWkt = _connection.CoordServices.ChooseCSDialog(DialogTitle, currentWkt ?? string.Empty);

        return string.IsNullOrWhiteSpace(chosenWkt) ? null : Describe(chosenWkt);
    }
}

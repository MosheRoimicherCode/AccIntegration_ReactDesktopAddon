namespace AccIntegration.Core.Models;

/// <summary>
/// Where an IFC is placed: pivot coordinates (meters) and coordinate system.
/// Read from the IFC, shown to the user, possibly edited, then used for the conversion.
/// </summary>
public sealed class IfcPlacement
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }

    // False when the IFC had no pivot and X/Y are 0 placeholders for the user to fill in.
    public bool HasPivot { get; init; }

    // Null when the IFC has no coordinate system; the user must choose one before converting.
    public CoordinateSystemInfo? CoordinateSystem { get; init; }
}

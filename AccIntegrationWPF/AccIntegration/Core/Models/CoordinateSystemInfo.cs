namespace AccIntegration.Core.Models;

public sealed class CoordinateSystemInfo
{
    public string Wkt { get; init; } = string.Empty;

    // Readable name for the UI (TerraExplorer's WKT description).
    public string Name { get; init; } = string.Empty;
}

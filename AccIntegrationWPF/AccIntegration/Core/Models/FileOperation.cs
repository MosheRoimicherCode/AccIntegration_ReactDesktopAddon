namespace AccIntegration.Core.Models;

/// <summary>
/// What should be done with a local file, decided by its extension.
/// </summary>
public enum FileOperation
{
    // Extension is not mapped; the caller should raise an error.
    Unsupported = 0,

    ConvertIfcTo3dml,

    ImportToTerraExplorer,

    OpenWithDefaultApp,

    // Kept on disk with no action (e.g. shapefile sidecars loaded together with the .shp).
    StoreOnly
}

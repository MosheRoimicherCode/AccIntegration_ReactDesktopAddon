using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.Import;

/// <summary>
/// TIFF as imagery or elevation (chosen by the import mode) and ECW as imagery.
/// Bounds, GroupID, Description, HScale and HOffset are read from ClientData.
/// </summary>
public sealed class RasterLayerImporter : ILayerImporter
{
    private readonly TerraExplorerConnection _connection;

    public RasterLayerImporter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".tif", ".tiff", ".ecw" };

    public void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        if (string.Equals(Path.GetExtension(filePath), ".ecw", StringComparison.OrdinalIgnoreCase))
        {
            ImportEcw(filePath, clientData);
            return;
        }

        ImportTiff(filePath, importMode, clientData);
    }

    private void ImportEcw(string filePath, IReadOnlyDictionary<string, string> clientData)
    {
        try
        {
            ImportImagery(filePath, clientData);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw new NotSupportedException(
                "ECW import failed with generic imagery settings. " +
                "Provide TerraExplorer developer import details for ECW (required plug name/init params/bounds).",
                ex);
        }
    }

    private void ImportTiff(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        try
        {
            switch (importMode)
            {
                case ImportMode.ImageLayer:
                    ImportImagery(filePath, clientData);
                    return;
                case ImportMode.ElevationLayer:
                    ImportElevation(filePath, clientData);
                    return;
                default:
                    throw new ArgumentException(
                        "TIFF import requires ImportMode ImageLayer or ElevationLayer.",
                        nameof(importMode));
            }
        }
        catch (Exception ex) when (ex is not NotSupportedException and not ArgumentException)
        {
            throw new NotSupportedException(
                $"TIFF import failed for mode '{importMode}'. Provide TerraExplorer raster/elevation import details (plug name/init params/bounds).",
                ex);
        }
    }

    private void ImportImagery(string filePath, IReadOnlyDictionary<string, string> clientData)
    {
        ITerrainRasterLayer81 layer = _connection.Creator.CreateImageryLayer(
                filePath,
                ClientDataValues.GetDouble(clientData, "UpperLeftX", 0),
                ClientDataValues.GetDouble(clientData, "UpperLeftY", 0),
                ClientDataValues.GetDouble(clientData, "LowerRightX", 0),
                ClientDataValues.GetDouble(clientData, "LowerRightY", 0),
                string.Empty,
                string.Empty,
                ClientDataValues.GetString(clientData, "GroupID", string.Empty),
                ClientDataValues.GetString(clientData, "Description", Path.GetFileName(filePath)))
            ?? throw new InvalidOperationException("CreateImageryLayer returned null.");

        layer.Refresh(0);
        ClientDataValues.ApplyTo(layer, clientData);
    }

    private void ImportElevation(string filePath, IReadOnlyDictionary<string, string> clientData)
    {
        ITerrainRasterLayer81 layer = _connection.Creator.CreateElevationLayer(
                filePath,
                ClientDataValues.GetDouble(clientData, "UpperLeftX", 0),
                ClientDataValues.GetDouble(clientData, "UpperLeftY", 0),
                ClientDataValues.GetDouble(clientData, "LowerRightX", 0),
                ClientDataValues.GetDouble(clientData, "LowerRightY", 0),
                string.Empty,
                string.Empty,
                ClientDataValues.GetString(clientData, "GroupID", string.Empty),
                ClientDataValues.GetString(clientData, "Description", Path.GetFileName(filePath)),
                ClientDataValues.GetDouble(clientData, "HScale", 1),
                ClientDataValues.GetDouble(clientData, "HOffset", 0))
            ?? throw new InvalidOperationException("CreateElevationLayer returned null.");

        layer.Refresh(0);
        ClientDataValues.ApplyTo(layer, clientData);
    }
}

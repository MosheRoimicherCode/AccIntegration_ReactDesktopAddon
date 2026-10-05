using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.Import;

/// <summary>
/// Shapefiles loaded as OGR feature layers. The .shx and .dbf must sit next to the .shp.
/// </summary>
public sealed class ShapefileLayerImporter : ILayerImporter
{
    private readonly TerraExplorerConnection _connection;

    public ShapefileLayerImporter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".shp" };

    public void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        EnsureSidecarsExist(filePath);

        string connectionString = $"FileName={filePath};TEPlugName=OGR;";

        IFeatureLayer81 layer = _connection.Creator.CreateFeatureLayer(
                Path.GetFileNameWithoutExtension(filePath),
                connectionString,
                null!)
            ?? throw new InvalidOperationException("CreateFeatureLayer returned null for SHP import.");

        layer.Refresh();
        ClientDataValues.ApplyTo(layer, clientData);
        _connection.World.Navigate.FlyTo(layer);
    }

    private static void EnsureSidecarsExist(string shpFilePath)
    {
        string directory = Path.GetDirectoryName(shpFilePath)
            ?? throw new InvalidOperationException($"Shapefile import failed: could not resolve directory for '{shpFilePath}'.");

        string baseName = Path.GetFileNameWithoutExtension(shpFilePath);
        string storageRoot = Directory.GetParent(directory)?.FullName ?? directory;
        string[] missing = new[] { ".shx", ".dbf" }
            .Where(extension => !EnsureSidecarAvailable(directory, storageRoot, baseName, extension))
            .Select(extension => $"{baseName}{extension}")
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Shapefile import failed: missing required sidecar file(s) {string.Join(", ", missing)} next to '{Path.GetFileName(shpFilePath)}'.");
        }
    }

    private static bool EnsureSidecarAvailable(
        string shapefileDirectory,
        string storageRoot,
        string baseName,
        string extension)
    {
        string sidecarName = $"{baseName}{extension}";
        string colocatedPath = Path.Combine(shapefileDirectory, sidecarName);
        if (File.Exists(colocatedPath))
            return true;

        // Older versions kept every extension in its own folder. Check that layout
        // so previously downloaded shapefile sets continue to import.
        string legacyPath = Path.Combine(
            storageRoot,
            extension.TrimStart('.').ToUpperInvariant(),
            sidecarName);
        if (!File.Exists(legacyPath))
            return false;

        File.Copy(legacyPath, colocatedPath, overwrite: false);
        return true;
    }
}

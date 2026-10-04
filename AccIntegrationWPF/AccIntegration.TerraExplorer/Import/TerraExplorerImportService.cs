using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;

namespace AccIntegration.TerraExplorer.Import;

/// <summary>
/// Picks the layer importer by file extension and imports the file with its ClientData.
/// IFC is not handled here; conversion to 3DML belongs to the WPF UI.
/// </summary>
public sealed class TerraExplorerImportService : ITerraExplorerImportService
{
    private readonly Dictionary<string, ILayerImporter> _importers;

    public TerraExplorerImportService(TerraExplorerConnection connection)
        : this(new ILayerImporter[]
        {
            new MeshLayerImporter(connection),
            new OgrFeatureLayerImporter(connection),
            new ShapefileLayerImporter(connection),
            new KmlLayerImporter(connection),
            new RasterLayerImporter(connection),
            new FlyLayerImporter(connection)
        })
    {
    }

    public TerraExplorerImportService(IEnumerable<ILayerImporter> importers)
    {
        _importers = new Dictionary<string, ILayerImporter>(StringComparer.OrdinalIgnoreCase);
        foreach (ILayerImporter importer in importers)
        {
            foreach (string extension in importer.Extensions)
            {
                _importers.Add(extension, importer);
            }
        }
    }

    public Task ImportAsync(
        DownloadedFileMetadata metadata,
        ImportMode importMode,
        CancellationToken cancellationToken = default)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        cancellationToken.ThrowIfCancellationRequested();

        string filePath = metadata.LocalPath;
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Import path is required.", nameof(metadata));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Import file was not found.", filePath);

        string extension = Path.GetExtension(filePath);
        if (!_importers.TryGetValue(extension, out ILayerImporter? importer))
        {
            throw new NotSupportedException(
                $"TerraExplorer import is not implemented for extension '{extension}'. " +
                $"Supported: {string.Join(", ", _importers.Keys)}.");
        }

        // COM calls run synchronously on the caller's thread.
        importer.Import(filePath, importMode, ClientDataBuilder.Build(metadata));
        return Task.CompletedTask;
    }
}

using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Imports a local file into TerraExplorer as a layer. The metadata supplies the file path
/// and the properties written to the layer as ClientData.
/// </summary>
public interface ITerraExplorerImportService
{
    Task ImportAsync(
        DownloadedFileMetadata metadata,
        ImportMode importMode,
        CancellationToken cancellationToken = default);
}

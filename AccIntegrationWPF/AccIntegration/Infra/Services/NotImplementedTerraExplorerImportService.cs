using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Used when no TerraExplorer integration is supplied to the factory (e.g. tests).
/// </summary>
public sealed class NotImplementedTerraExplorerImportService : ITerraExplorerImportService
{
    public Task ImportAsync(
        DownloadedFileMetadata metadata,
        ImportMode importMode,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"TerraExplorer import is not available. The file is stored at '{metadata.LocalPath}'.");
    }
}

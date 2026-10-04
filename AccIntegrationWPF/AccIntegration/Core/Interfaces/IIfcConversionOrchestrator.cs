using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Local processor flow for a handed-over IFC:
/// 1) <see cref="PrepareAsync"/> reads its placement for the user to review,
/// 2) <see cref="RunAsync"/> converts with the confirmed placement, saves the 3DML metadata
///    and imports it into TerraExplorer.
/// </summary>
public interface IIfcConversionOrchestrator
{
    Task<IfcPlacement> PrepareAsync(string ifcPath, CancellationToken cancellationToken = default);

    /// <param name="progress">Conversion progress, 0-100.</param>
    /// <returns>The metadata of the new 3DML.</returns>
    Task<DownloadedFileMetadata> RunAsync(
        string ifcPath,
        string outputPath,
        IfcPlacement placement,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Saves metadata for a 3DML converted from a stored IFC, under the IFC's project/version/file URNs,
/// so later runs find the 3DML instead of converting again.
/// </summary>
public interface IConverted3dmlRegistrar
{
    /// <summary>
    /// Returns the metadata record of the stored IFC at this path, or null when it is not stored.
    /// </summary>
    Task<DownloadedFileMetadata?> FindIfcAsync(string ifcPath, CancellationToken cancellationToken = default);

    Task<DownloadedFileMetadata> RegisterAsync(
        DownloadedFileMetadata ifcMetadata,
        string outputPath,
        CancellationToken cancellationToken = default);
}

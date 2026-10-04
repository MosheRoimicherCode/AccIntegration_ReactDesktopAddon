using AccIntegration.Core.Models;

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Checks whether files described by metadata exist on the local disk.
/// </summary>
public interface IQueryLocalFiles
{
    bool Exists(DownloadedFileMetadata metadata);
}

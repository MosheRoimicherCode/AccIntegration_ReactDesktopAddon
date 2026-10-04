using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class QueryLocalFiles : IQueryLocalFiles
{
    public bool Exists(DownloadedFileMetadata metadata)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        return !string.IsNullOrWhiteSpace(metadata.LocalPath) && File.Exists(metadata.LocalPath);
    }
}

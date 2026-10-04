namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Downloads a remote file to a given local path.
/// </summary>
public interface IDownloadedFileService
{
    /// <returns>
    /// The file name suggested by the server (Content-Disposition, else the URL's last segment), or null.
    /// </returns>
    Task<string?> DownloadFileAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

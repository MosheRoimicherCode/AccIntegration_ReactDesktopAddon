using System.Diagnostics;
using AccIntegration.Core.Interfaces;

namespace AccIntegration.Infra.Services;

public sealed class OpenFileService : IOpenFileService
{
    public bool Open(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File to open was not found.", filePath);

        // UseShellExecute lets Windows pick the app registered for the extension.
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });

        return process is not null;
    }
}

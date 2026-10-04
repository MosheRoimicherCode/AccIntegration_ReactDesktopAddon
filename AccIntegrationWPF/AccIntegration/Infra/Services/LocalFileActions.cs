using System.Diagnostics;
using AccIntegration.Core.Interfaces;

namespace AccIntegration.Infra.Services;

public sealed class LocalFileActions : ILocalFileActions
{
    public bool ShowInFolder(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File to show was not found.", filePath);

        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = true
        });

        return process is not null;
    }

    public bool Delete(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        File.Delete(filePath);
        return true;
    }
}

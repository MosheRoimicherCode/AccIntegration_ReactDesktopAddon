using System.Text;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Guesses a file's extension from its first bytes, for downloads whose name has no usable extension
/// (e.g. ACC's cloud-converted IFC for RVT items). Same signatures TEZ used.
/// </summary>
internal static class FileTypeSniffer
{
    public static string? TryGetExtension(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[4096];
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read < 4)
                return null;

            if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46) // %PDF
                return ".pdf";

            if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04) // PK..
                return ".zip";

            string text = Encoding.ASCII.GetString(buffer, 0, read);
            if (text.Contains("ISO-10303-21", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("IFC", StringComparison.OrdinalIgnoreCase))
            {
                return ".ifc";
            }
        }
        catch (IOException)
        {
            // Unreadable: no guess.
        }

        return null;
    }
}

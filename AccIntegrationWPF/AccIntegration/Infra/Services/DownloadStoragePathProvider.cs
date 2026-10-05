using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class DownloadStoragePathProvider : IDownloadStoragePathProvider
{
    private static readonly HashSet<char> InvalidChars = Path.GetInvalidFileNameChars().ToHashSet();
    private static readonly HashSet<string> ShapefileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".shp", ".shx", ".dbf", ".prj", ".cpg", ".qix", ".sbn", ".sbx"
    };

    public string AppFolderPath { get; }

    public string DownloadedFilesRootPath { get; }

    public string MetadataDatabaseFolderPath { get; }

    public DownloadStoragePathProvider(DownloadedFileStorageOptions? options = null)
    {
        options ??= new DownloadedFileStorageOptions();

        AppFolderPath = options.ResolveAppFolderPath();
        DownloadedFilesRootPath = options.ResolveDownloadedFilesFolderPath();
        MetadataDatabaseFolderPath = options.ResolveMetadataDatabaseFolder();

        Directory.CreateDirectory(AppFolderPath);
        Directory.CreateDirectory(DownloadedFilesRootPath);
        Directory.CreateDirectory(MetadataDatabaseFolderPath);
    }

    public string EnsureFolderForExtension(string? extension)
    {
        string folderName = NormalizeExtensionFolderName(extension);
        string folderPath = Path.Combine(DownloadedFilesRootPath, folderName);
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static string NormalizeExtensionFolderName(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "OTHER";

        ReadOnlySpan<char> span = extension.AsSpan().Trim();

        if (ShapefileExtensions.Contains(span.ToString()))
            return "SHP";

        if (span.StartsWith("."))
            span = span[1..];

        if (span.IsWhiteSpace())
            return "OTHER";

        Span<char> destination = stackalloc char[span.Length];
        int written = 0;

        foreach (char c in span)
        {
            if (!InvalidChars.Contains(c))
                destination[written++] = char.ToUpperInvariant(c);
        }

        if (written == 0)
            return "OTHER";

        return new string(destination[..written]);
    }
}

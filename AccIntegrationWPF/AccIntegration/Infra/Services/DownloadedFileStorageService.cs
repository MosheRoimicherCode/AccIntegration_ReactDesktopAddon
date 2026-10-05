using System.Text;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class DownloadedFileStorageService : IDownloadedFileStorageService
{
    public const string ProjectUrnPropertyName = "ProjectUrn";
    public const string VersionUrnPropertyName = "VersionUrn";
    public const string FileUrnPropertyName = "FileUrn";

    // Unnamed downloads land here until the server tells us the file name.
    private const string IncomingFolderName = ".incoming";

    private readonly IDownloadStoragePathProvider _pathProvider;
    private readonly IDownloadedFileService _downloadedFileService;
    private readonly IDownloadMetadataService _metadataService;
    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly IQueryLocalFiles _localFiles;

    public DownloadedFileStorageService(
        IDownloadStoragePathProvider pathProvider,
        IDownloadedFileService downloadedFileService,
        IDownloadMetadataService metadataService,
        IQueryLocalMetadata metadataQuery,
        IQueryLocalFiles localFiles)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _downloadedFileService = downloadedFileService ?? throw new ArgumentNullException(nameof(downloadedFileService));
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _localFiles = localFiles ?? throw new ArgumentNullException(nameof(localFiles));
    }

    public async Task<LocalFileResult> GetOrDownloadAsync(
        string fileUrl,
        string? fileName,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException("File URL is required.", nameof(fileUrl));

        ValidateUrn(projectUrn, nameof(projectUrn));
        ValidateUrn(versionUrn, nameof(versionUrn));
        ValidateUrn(fileUrn, nameof(fileUrn));

        string? requestedExtension = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetExtension(fileName);
        DownloadedFileMetadata? local = await FindLocalFileAsync(
                projectUrn,
                versionUrn,
                fileUrn,
                requestedExtension,
                cancellationToken)
            .ConfigureAwait(false);

        if (local is not null)
            return ToResult(local, usedLocalFile: true);

        DownloadedFileMetadata downloaded = string.IsNullOrWhiteSpace(fileName)
            ? await DownloadUnnamedAndStoreAsync(fileUrl, projectUrn, versionUrn, fileUrn, properties, cancellationToken)
                .ConfigureAwait(false)
            : await DownloadAndStoreAsync(fileUrl, fileName, projectUrn, versionUrn, fileUrn, properties, cancellationToken)
                .ConfigureAwait(false);

        return ToResult(downloaded, usedLocalFile: false);
    }

    public async Task<LocalFileResult> StoreLocalFileAsync(
        string sourcePath,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("File path is required.", nameof(sourcePath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The file to import was not found.", sourcePath);

        ValidateUrn(projectUrn, nameof(projectUrn));
        ValidateUrn(versionUrn, nameof(versionUrn));
        ValidateUrn(fileUrn, nameof(fileUrn));

        string sourceFullPath = Path.GetFullPath(sourcePath);
        string storedPath;

        // Already in managed storage: register it where it is instead of copying it again.
        if (IsInsideManagedStorage(sourceFullPath))
        {
            storedPath = sourceFullPath;
        }
        else
        {
            string fileName = SanitizeFileName(Path.GetFileName(sourceFullPath));
            string folderPath = _pathProvider.EnsureFolderForExtension(Path.GetExtension(fileName));
            storedPath = UniqueFilePath.Build(folderPath, fileName);

            try
            {
                await using var source = new FileStream(sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var target = new FileStream(storedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryDelete(storedPath);
                throw;
            }
        }

        DownloadedFileMetadata metadata = BuildMetadata(
            sourceFullPath,
            Path.GetFileName(sourceFullPath),
            storedPath,
            projectUrn,
            versionUrn,
            fileUrn,
            properties);

        DownloadedFileMetadata created = await _metadataService.CreateAsync(metadata, cancellationToken).ConfigureAwait(false);
        return ToResult(created, usedLocalFile: false);
    }

    /// <summary>
    /// Searches only the requested file's records, removes stale records for that file,
    /// and returns a matching extension (or its converted 3DML for an IFC request).
    /// </summary>
    private async Task<DownloadedFileMetadata?> FindLocalFileAsync(
        string projectUrn,
        string versionUrn,
        string fileUrn,
        string? requestedExtension,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DownloadedFileMetadata> records = await _metadataQuery
            .GetAllByProjectAndVersionAsync(projectUrn, versionUrn, cancellationToken)
            .ConfigureAwait(false);

        DownloadedFileMetadata[] requestedRecords = records
            .Where(record => string.Equals(record.FileUrn, fileUrn, StringComparison.Ordinal))
            .ToArray();

        var existing = new List<DownloadedFileMetadata>();
        foreach (DownloadedFileMetadata record in requestedRecords)
        {
            if (_localFiles.Exists(record))
            {
                existing.Add(record);
                continue;
            }

            await _metadataService.DeleteAsync(record.Id, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(requestedExtension, ".ifc", StringComparison.OrdinalIgnoreCase))
            return existing.FirstOrDefault(record => Is3dml(record.LocalPath)) ??
                   existing.FirstOrDefault(record => HasExtension(record.LocalPath, requestedExtension));

        return string.IsNullOrWhiteSpace(requestedExtension)
            ? existing.FirstOrDefault(record => Is3dml(record.LocalPath)) ?? existing.FirstOrDefault()
            : existing.FirstOrDefault(record => HasExtension(record.LocalPath, requestedExtension));
    }

    private static bool HasExtension(string path, string? extension) =>
        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    private async Task<DownloadedFileMetadata> DownloadAndStoreAsync(
        string fileUrl,
        string fileName,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken)
    {
        string safeFileName = SanitizeFileName(fileName);
        string folderPath = _pathProvider.EnsureFolderForExtension(Path.GetExtension(safeFileName));
        string destinationPath = UniqueFilePath.Build(folderPath, safeFileName);

        try
        {
            await _downloadedFileService
                .DownloadFileAsync(fileUrl, destinationPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            TryDelete(destinationPath);
            throw;
        }

        DownloadedFileMetadata metadata = BuildMetadata(
            fileUrl,
            fileName,
            destinationPath,
            projectUrn,
            versionUrn,
            fileUrn,
            properties);

        return await _metadataService.CreateAsync(metadata, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// No name from the caller: download to a temporary file, take the server's name
    /// (or guess the extension from the content), then move it into its type folder.
    /// </summary>
    private async Task<DownloadedFileMetadata> DownloadUnnamedAndStoreAsync(
        string fileUrl,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken)
    {
        string incomingFolder = Path.Combine(_pathProvider.DownloadedFilesRootPath, IncomingFolderName);
        Directory.CreateDirectory(incomingFolder);
        string temporaryPath = Path.Combine(incomingFolder, $"{Guid.NewGuid():N}.download");

        string destinationPath;
        string originalFileName;
        try
        {
            string? serverFileName = await _downloadedFileService
                .DownloadFileAsync(fileUrl, temporaryPath, cancellationToken)
                .ConfigureAwait(false);

            originalFileName = SanitizeFileName(serverFileName ?? string.Empty);
            string fileName = WithContentExtensionIfMissing(originalFileName, temporaryPath);

            string folderPath = _pathProvider.EnsureFolderForExtension(Path.GetExtension(fileName));
            destinationPath = UniqueFilePath.Build(folderPath, fileName);
            File.Move(temporaryPath, destinationPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }

        DownloadedFileMetadata metadata = BuildMetadata(
            fileUrl,
            originalFileName,
            destinationPath,
            projectUrn,
            versionUrn,
            fileUrn,
            properties);

        return await _metadataService.CreateAsync(metadata, cancellationToken).ConfigureAwait(false);
    }

    private static string WithContentExtensionIfMissing(string fileName, string contentPath)
    {
        string extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension) &&
            !string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        string? sniffed = FileTypeSniffer.TryGetExtension(contentPath);
        return sniffed is null ? fileName : $"{Path.GetFileNameWithoutExtension(fileName)}{sniffed}";
    }

    private bool IsInsideManagedStorage(string fullPath)
    {
        string root = Path.GetFullPath(_pathProvider.DownloadedFilesRootPath)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalFileResult ToResult(DownloadedFileMetadata metadata, bool usedLocalFile)
    {
        return new LocalFileResult
        {
            FilePath = metadata.LocalPath,
            Metadata = metadata,
            UsedLocalFile = usedLocalFile,
            Downloaded = !usedLocalFile,
            Is3dml = Is3dml(metadata.LocalPath)
        };
    }

    private static DownloadedFileMetadata BuildMetadata(
        string sourceReference,
        string originalFileName,
        string localFilePath,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        Dictionary<string, string>? properties)
    {
        string extension = Path.GetExtension(localFilePath).TrimStart('.').ToUpperInvariant();
        string folderName = Path.GetFileName(Path.GetDirectoryName(localFilePath) ?? string.Empty);
        string storedFileName = Path.GetFileName(localFilePath);

        var copiedProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (properties is not null)
        {
            foreach ((string key, string value) in properties)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    copiedProperties[key] = value;
            }
        }

        copiedProperties[ProjectUrnPropertyName] = projectUrn;
        copiedProperties[VersionUrnPropertyName] = versionUrn;
        copiedProperties[FileUrnPropertyName] = fileUrn;

        return new DownloadedFileMetadata
        {
            Id = Guid.NewGuid(),
            ProjectUrn = projectUrn,
            VersionUrn = versionUrn,
            FileUrn = fileUrn,
            FileName = storedFileName,
            SourceReference = sourceReference,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            Extension = string.IsNullOrWhiteSpace(extension) ? "OTHER" : extension,
            TypeFolderName = string.IsNullOrWhiteSpace(folderName) ? "OTHER" : folderName,
            LocalPath = localFilePath,
            SizeBytes = new FileInfo(localFilePath).Length,
            DownloadedAtUtc = DateTime.UtcNow,
            Properties = copiedProperties
        };
    }

    private static bool Is3dml(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".3dml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".o3dml", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateUrn(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"A non-empty '{parameterName}' is required.", parameterName);
    }

    private static string SanitizeFileName(string value)
    {
        string rawValue = value.Trim().Trim('"');
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(rawValue.Length);

        foreach (char character in rawValue)
        {
            builder.Append(Array.IndexOf(invalidFileNameChars, character) >= 0 ? '_' : character);
        }

        string sanitized = builder.ToString();
        return string.IsNullOrWhiteSpace(sanitized)
            ? $"file_{Guid.NewGuid():N}"
            : sanitized;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup failures; the original error is rethrown.
        }
    }
}

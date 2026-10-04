using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class Converted3dmlRegistrar : IConverted3dmlRegistrar
{
    public const string ArtifactTypePropertyName = "ArtifactType";
    public const string AccIfcPathPropertyName = "AccIfcPath";

    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly IDownloadMetadataService _metadataService;

    public Converted3dmlRegistrar(IQueryLocalMetadata metadataQuery, IDownloadMetadataService metadataService)
    {
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
    }

    public async Task<DownloadedFileMetadata?> FindIfcAsync(string ifcPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ifcPath))
            throw new ArgumentException("IFC path is required.", nameof(ifcPath));

        string fullPath = Path.GetFullPath(ifcPath);
        IReadOnlyList<DownloadedFileMetadata> records = await _metadataQuery
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.FirstOrDefault(record =>
            !string.IsNullOrWhiteSpace(record.LocalPath) &&
            string.Equals(Path.GetFullPath(record.LocalPath), fullPath, StringComparison.OrdinalIgnoreCase));
    }

    public Task<DownloadedFileMetadata> RegisterAsync(
        DownloadedFileMetadata ifcMetadata,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (ifcMetadata is null)
            throw new ArgumentNullException(nameof(ifcMetadata));

        if (!File.Exists(outputPath))
            throw new FileNotFoundException("Converted 3DML was not found.", outputPath);

        // Same identity as the IFC; AccIfcPath links the layer back to its source IFC (TEZ ClientData).
        var properties = new Dictionary<string, string>(ifcMetadata.Properties, StringComparer.OrdinalIgnoreCase)
        {
            [ArtifactTypePropertyName] = "3DML",
            [AccIfcPathPropertyName] = ifcMetadata.LocalPath
        };

        string fileName = Path.GetFileName(outputPath);
        var metadata = new DownloadedFileMetadata
        {
            Id = Guid.NewGuid(),
            ProjectUrn = ifcMetadata.ProjectUrn,
            VersionUrn = ifcMetadata.VersionUrn,
            FileUrn = ifcMetadata.FileUrn,
            FileName = fileName,
            SourceReference = ifcMetadata.LocalPath,
            OriginalFileName = ifcMetadata.FileName,
            StoredFileName = fileName,
            Extension = "3DML",
            TypeFolderName = Path.GetFileName(Path.GetDirectoryName(outputPath) ?? string.Empty),
            LocalPath = outputPath,
            SizeBytes = new FileInfo(outputPath).Length,
            DownloadedAtUtc = DateTime.UtcNow,
            Properties = properties
        };

        return _metadataService.CreateAsync(metadata, cancellationToken);
    }
}

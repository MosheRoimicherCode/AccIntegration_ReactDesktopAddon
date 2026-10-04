using AccIntegration.Core.Interfaces;

namespace AccIntegration.Infra;

/// <summary>
/// The brain's services, created together by <see cref="AccIntegrationFactory"/>.
/// </summary>
public sealed class AccIntegrationContext
{
    public required IDownloadStoragePathProvider PathProvider { get; init; }

    public required IQueryLocalMetadata MetadataQuery { get; init; }

    public required IDownloadMetadataService MetadataService { get; init; }

    public required IQueryLocalFiles LocalFiles { get; init; }

    public required IDownloadedFileStorageService Storage { get; init; }

    public required IFileOrchestrator Files { get; init; }

    public required IIfcConversionOrchestrator IfcConversion { get; init; }

    public required ICoordinateSystemService CoordinateSystems { get; init; }

    public required ILocalFileActions FileActions { get; init; }
}

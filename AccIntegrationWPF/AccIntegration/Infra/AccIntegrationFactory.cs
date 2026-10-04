using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegration.Infra.Services;

namespace AccIntegration.Infra;

/// <summary>
/// Creates the brain's services with their dependencies. Shared by the CLI and the WPF host.
/// </summary>
public static class AccIntegrationFactory
{
    public static AccIntegrationContext Create(
        ITerraExplorerImportService? terraExplorerImport = null,
        IIfcConversionLauncher? ifcConversionLauncher = null,
        IIfcTo3dmlConverter? ifcTo3dmlConverter = null,
        IIfcPlacementReader? ifcPlacementReader = null,
        ICoordinateSystemService? coordinateSystems = null)
    {
        terraExplorerImport ??= new NotImplementedTerraExplorerImportService();
        coordinateSystems ??= new NotImplementedCoordinateSystemService();

        IDownloadStoragePathProvider pathProvider = new DownloadStoragePathProvider(new DownloadedFileStorageOptions());
        IQueryLocalMetadata metadataQuery = new QueryLocalMetadata(pathProvider);
        IDownloadMetadataService metadataService = new DownloadMetadataService(pathProvider);
        IQueryLocalFiles localFiles = new QueryLocalFiles();

        IDownloadedFileStorageService storage = new DownloadedFileStorageService(
            pathProvider,
            new DownloadedFileService(),
            metadataService,
            metadataQuery,
            localFiles);

        IFileOrchestrator files = new FileOrchestrator(
            pathProvider,
            storage,
            new OpenFileService(),
            terraExplorerImport,
            ifcConversionLauncher ?? new NotImplementedIfcConversionLauncher());

        IIfcConversionOrchestrator ifcConversion = new IfcConversionOrchestrator(
            ifcPlacementReader ?? new NotImplementedIfcPlacementReader(),
            coordinateSystems,
            ifcTo3dmlConverter ?? new NotImplementedIfcTo3dmlConverter(),
            new Converted3dmlRegistrar(metadataQuery, metadataService),
            terraExplorerImport);

        return new AccIntegrationContext
        {
            PathProvider = pathProvider,
            MetadataQuery = metadataQuery,
            MetadataService = metadataService,
            LocalFiles = localFiles,
            Storage = storage,
            Files = files,
            IfcConversion = ifcConversion,
            CoordinateSystems = coordinateSystems,
            FileActions = new LocalFileActions()
        };
    }
}

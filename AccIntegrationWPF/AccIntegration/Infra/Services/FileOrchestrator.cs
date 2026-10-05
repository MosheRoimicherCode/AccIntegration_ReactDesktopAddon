using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class FileOrchestrator : IFileOrchestrator
{
    private readonly IDownloadStoragePathProvider _pathProvider;
    private readonly IDownloadedFileStorageService _storageService;
    private readonly IOpenFileService _openFileService;
    private readonly ITerraExplorerImportService _terraExplorerImportService;
    private readonly IIfcConversionLauncher _ifcConversionLauncher;

    public FileOrchestrator(
        IDownloadStoragePathProvider pathProvider,
        IDownloadedFileStorageService storageService,
        IOpenFileService openFileService,
        ITerraExplorerImportService terraExplorerImportService,
        IIfcConversionLauncher ifcConversionLauncher)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _openFileService = openFileService ?? throw new ArgumentNullException(nameof(openFileService));
        _terraExplorerImportService = terraExplorerImportService ?? throw new ArgumentNullException(nameof(terraExplorerImportService));
        _ifcConversionLauncher = ifcConversionLauncher ?? throw new ArgumentNullException(nameof(ifcConversionLauncher));
    }

    public async Task<FileOperationResult> RunAsync(
        string fileUrl,
        string? fileName,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        ImportMode importMode = ImportMode.Default,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        // Fail before downloading when the requested file can never be handled.
        // Without a name the type is only known after the download.
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            EnsureSupported(fileName);
            EnsureTiffModeSelected(fileName, importMode);
        }

        LocalFileResult file = await _storageService
            .GetOrDownloadAsync(fileUrl, fileName, projectUrn, versionUrn, fileUrn, properties, cancellationToken)
            .ConfigureAwait(false);

        return await ExecuteAsync(file, importMode, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileOperationResult> RunLocalFileAsync(
        string sourcePath,
        string projectUrn,
        string versionUrn,
        string fileUrn,
        ImportMode importMode = ImportMode.Default,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(sourcePath);
        EnsureTiffModeSelected(sourcePath, importMode);

        LocalFileResult file = await _storageService
            .StoreLocalFileAsync(sourcePath, projectUrn, versionUrn, fileUrn, properties, cancellationToken)
            .ConfigureAwait(false);

        return await ExecuteAsync(file, importMode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the operation mapped to the stored file's type.
    /// </summary>
    private async Task<FileOperationResult> ExecuteAsync(
        LocalFileResult file,
        ImportMode importMode,
        CancellationToken cancellationToken)
    {
        // The local file can differ from the requested name (e.g. a 3DML found for an IFC).
        FileOperation operation = FileOperationMap.Resolve(file.FilePath);
        EnsureSupported(file.FilePath);
        EnsureTiffModeSelected(file.FilePath, importMode);

        string? outputPath = null;
        AccIntegration.Core.Models.IfcConversionResult? ifcConversionResult = null;

        switch (operation)
        {
            case FileOperation.ImportToTerraExplorer:
                try
                {
                    await _terraExplorerImportService
                        .ImportAsync(file.Metadata, importMode, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new LayerImportException(file.FilePath, ex);
                }
                break;

            case FileOperation.OpenWithDefaultApp:
                _openFileService.Open(file.FilePath);
                break;

            case FileOperation.StoreOnly:
                break;

            case FileOperation.ConvertIfcTo3dml:
                outputPath = Resolve3dmlOutputPath(file.FilePath);
                try
                {
                    ifcConversionResult = await _ifcConversionLauncher
                        .LaunchAsync(file.FilePath, outputPath, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ifcConversionResult = new IfcConversionResult
                    {
                        Success = false,
                        Status = ex is OperationCanceledException ? "cancelled" : "failed",
                        Message = ex is OperationCanceledException ? "Cancelled by user." : ex.Message,
                        FailureException = ex
                    };
                }
                break;

            default:
                throw new NotSupportedException($"No operation is mapped for '{file.FilePath}'.");
        }

        return new FileOperationResult
        {
            Operation = operation,
            ImportMode = importMode,
            File = file,
            OutputPath = outputPath,
            IfcConversionResult = ifcConversionResult
        };
    }

    /// <summary>
    /// Downloaded Files\3DML\{ifc name}.3dml, with a _1, _2... suffix when the name is taken.
    /// </summary>
    private string Resolve3dmlOutputPath(string ifcPath)
    {
        string folderPath = _pathProvider.EnsureFolderForExtension(".3dml");
        return UniqueFilePath.Build(folderPath, $"{Path.GetFileNameWithoutExtension(ifcPath)}.3dml");
    }

    private static void EnsureSupported(string pathOrFileName)
    {
        if (FileOperationMap.Resolve(pathOrFileName) == FileOperation.Unsupported)
        {
            throw new NotSupportedException(
                $"File type '{Path.GetExtension(pathOrFileName)}' is not supported ('{pathOrFileName}').");
        }
    }

    private static void EnsureTiffModeSelected(string pathOrFileName, ImportMode importMode)
    {
        if (IsTiff(pathOrFileName) && importMode == ImportMode.Default)
        {
            throw new ArgumentException(
                "TIFF import requires ImportMode ImageLayer or ElevationLayer.",
                nameof(importMode));
        }
    }

    private static bool IsTiff(string pathOrFileName)
    {
        string extension = Path.GetExtension(pathOrFileName);
        return string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase);
    }
}

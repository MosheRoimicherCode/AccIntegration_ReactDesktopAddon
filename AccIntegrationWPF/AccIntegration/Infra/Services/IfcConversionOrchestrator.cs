using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

public sealed class IfcConversionOrchestrator : IIfcConversionOrchestrator
{
    private readonly IIfcPlacementReader _placementReader;
    private readonly ICoordinateSystemService _coordinateSystems;
    private readonly IIfcTo3dmlConverter _converter;
    private readonly IConverted3dmlRegistrar _registrar;
    private readonly ITerraExplorerImportService _terraExplorerImportService;

    public IfcConversionOrchestrator(
        IIfcPlacementReader placementReader,
        ICoordinateSystemService coordinateSystems,
        IIfcTo3dmlConverter converter,
        IConverted3dmlRegistrar registrar,
        ITerraExplorerImportService terraExplorerImportService)
    {
        _placementReader = placementReader ?? throw new ArgumentNullException(nameof(placementReader));
        _coordinateSystems = coordinateSystems ?? throw new ArgumentNullException(nameof(coordinateSystems));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        _terraExplorerImportService = terraExplorerImportService ?? throw new ArgumentNullException(nameof(terraExplorerImportService));
    }

    public async Task<IfcPlacement> PrepareAsync(string ifcPath, CancellationToken cancellationToken = default)
    {
        // Checked before the user reviews anything: without the IFC record the 3DML could not be saved.
        await FindIfcRecordAsync(ifcPath, cancellationToken).ConfigureAwait(false);

        IfcPlacement placement = await _placementReader
            .ReadAsync(ifcPath, cancellationToken)
            .ConfigureAwait(false);

        if (placement.CoordinateSystem is not { Wkt.Length: > 0 } coordinateSystem)
            return placement;

        return new IfcPlacement
        {
            X = placement.X,
            Y = placement.Y,
            Z = placement.Z,
            HasPivot = placement.HasPivot,
            CoordinateSystem = _coordinateSystems.Describe(coordinateSystem.Wkt)
        };
    }

    public async Task<DownloadedFileMetadata> RunAsync(
        string ifcPath,
        string outputPath,
        IfcPlacement placement,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (placement is null)
            throw new ArgumentNullException(nameof(placement));

        if (string.IsNullOrWhiteSpace(placement.CoordinateSystem?.Wkt))
            throw new ArgumentException("A coordinate system is required to convert the IFC.", nameof(placement));

        DownloadedFileMetadata ifcMetadata = await FindIfcRecordAsync(ifcPath, cancellationToken).ConfigureAwait(false);

        await _converter
            .ConvertAsync(ifcPath, outputPath, placement, progress, cancellationToken)
            .ConfigureAwait(false);

        DownloadedFileMetadata converted = await _registrar
            .RegisterAsync(ifcMetadata, outputPath, cancellationToken)
            .ConfigureAwait(false);

        await _terraExplorerImportService
            .ImportAsync(converted, ImportMode.Default, cancellationToken)
            .ConfigureAwait(false);

        return converted;
    }

    private async Task<DownloadedFileMetadata> FindIfcRecordAsync(string ifcPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(ifcPath))
            throw new FileNotFoundException("IFC file was not found.", ifcPath);

        return await _registrar
            .FindIfcAsync(ifcPath, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No metadata record found for IFC '{ifcPath}'.");
    }
}

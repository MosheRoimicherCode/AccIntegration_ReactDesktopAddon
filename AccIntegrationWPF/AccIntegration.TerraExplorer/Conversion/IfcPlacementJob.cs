using _3DML.Api;
using AccIntegration.Core.Models;

namespace AccIntegration.TerraExplorer.Conversion;

/// <summary>
/// Reads the pivot and coordinate system stored in an IFC through Skyline's _3DMLCreator.
/// Kept apart so 3DML.Api only loads after <see cref="TerraExplorerAssemblies"/> ran.
/// </summary>
internal static class IfcPlacementJob
{
    public static Task<IfcPlacement> ReadAsync(string ifcPath, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("IFC placement reading requires Windows.");

        var completion = new TaskCompletionSource<IfcPlacement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IfcPlacement placement = Read(ifcPath);
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(placement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "IFC placement reader"
        };
        // Do not use Task.Run: the native 3DML object needs an STA, not a pool thread.
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        return completion.Task;
    }

    private static IfcPlacement Read(string ifcPath)
    {
        var creator = new _3DMLCreator();
        // LayerPivot is a struct; null when TerraExplorer found no placement at all.
        LayerPivot? result = creator.GetLayerPivot(ifcPath, 1.0);
        LayerPivot pivot = result.GetValueOrDefault();

        bool hasPivot = result.HasValue && pivot.X is not null && pivot.Y is not null;
        string? wkt = result.HasValue ? pivot.WKT : null;

        return new IfcPlacement
        {
            X = pivot.X ?? 0,
            Y = pivot.Y ?? 0,
            Z = pivot.Height ?? 0,
            HasPivot = hasPivot,
            // Named later by the coordinate system service.
            CoordinateSystem = string.IsNullOrWhiteSpace(wkt) ? null : new CoordinateSystemInfo { Wkt = wkt }
        };
    }
}

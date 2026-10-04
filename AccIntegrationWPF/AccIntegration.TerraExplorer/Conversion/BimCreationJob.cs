using _3DML.Api;
using AccIntegration.Core.Models;

namespace AccIntegration.TerraExplorer.Conversion;

/// <summary>
/// One 3DML creation through Skyline's _3DMLCreator at the placement confirmed by the user.
/// Kept apart from other classes so 3DML.Api only loads after <see cref="TerraExplorerAssemblies"/> ran.
/// </summary>
internal static class BimCreationJob
{
    public static async Task RunAsync(
        string ifcPath,
        string targetPath,
        IfcPlacement placement,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string wkt = placement.CoordinateSystem?.Wkt
            ?? throw new ArgumentException("A coordinate system is required.", nameof(placement));

        var creator = new _3DMLCreator();

        // OnFinished also fires after OnError, so the first error is kept and reported on finish.
        var finished = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        string? error = null;
        creator.OnProgress += value => progress?.Report(Math.Clamp(value, 0, 100));
        creator.OnError += message => Interlocked.CompareExchange(ref error, message, null);
        creator.OnFinished += () => finished.TrySetResult(Volatile.Read(ref error));

        creator.AddBIMDataSource(
            Path.GetFileNameWithoutExtension(ifcPath),
            ifcPath,
            wkt,
            placement.X,
            placement.Y,
            placement.Z,
            1.0,
            null!,
            AltitudeUnitTypeCode.Meters,
            string.Empty);
        creator.ProjectionWKT = wkt;

        creator.BeginCreation(targetPath);

        string? failure;
        using (cancellationToken.Register(() => creator.RequestAbort()))
        {
            failure = await finished.Task.ConfigureAwait(false);
        }

        GC.KeepAlive(creator);
        cancellationToken.ThrowIfCancellationRequested();

        if (failure is not null)
            throw new InvalidOperationException($"3DML creation failed: {failure}");
    }
}

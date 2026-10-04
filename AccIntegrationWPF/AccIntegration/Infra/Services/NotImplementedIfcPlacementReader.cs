using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Used when no placement reader is supplied to the factory (e.g. the CLI).
/// </summary>
public sealed class NotImplementedIfcPlacementReader : IIfcPlacementReader
{
    public Task<IfcPlacement> ReadAsync(string ifcPath, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Reading IFC placement is only available in the local processor.");
    }
}

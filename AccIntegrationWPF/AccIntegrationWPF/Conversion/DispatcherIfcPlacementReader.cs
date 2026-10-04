using System.Windows.Threading;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.Conversion;

/// <summary>
/// Starts placement preparation on the UI STA for TerraExplorer COM access.
/// The reader moves the slow 3DML file read to its own STA worker before returning a task.
/// </summary>
public sealed class DispatcherIfcPlacementReader : IIfcPlacementReader
{
    private readonly IIfcPlacementReader _inner;
    private readonly Dispatcher _dispatcher;

    public DispatcherIfcPlacementReader(IIfcPlacementReader inner, Dispatcher dispatcher)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<IfcPlacement> ReadAsync(string ifcPath, CancellationToken cancellationToken = default)
    {
        return _dispatcher
            .InvokeAsync(() => _inner.ReadAsync(ifcPath, cancellationToken))
            .Task
            .Unwrap();
    }
}

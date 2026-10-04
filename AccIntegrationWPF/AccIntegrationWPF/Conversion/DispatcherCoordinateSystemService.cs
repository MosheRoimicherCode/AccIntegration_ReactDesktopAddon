using System.Windows.Threading;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.Conversion;

/// <summary>
/// Runs TerraExplorer coordinate system calls (including its modal dialog) on the UI (STA) thread.
/// </summary>
public sealed class DispatcherCoordinateSystemService : ICoordinateSystemService
{
    private readonly ICoordinateSystemService _inner;
    private readonly Dispatcher _dispatcher;

    public DispatcherCoordinateSystemService(ICoordinateSystemService inner, Dispatcher dispatcher)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public CoordinateSystemInfo Describe(string wkt)
    {
        return _dispatcher.Invoke(() => _inner.Describe(wkt));
    }

    public CoordinateSystemInfo? Choose(string? currentWkt)
    {
        return _dispatcher.Invoke(() => _inner.Choose(currentWkt));
    }
}

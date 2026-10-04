using System.Windows.Threading;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.Conversion;

/// <summary>
/// Starts conversions on the UI (STA) thread so TerraExplorer COM and 3DML objects are created there,
/// even when the brain calls from a background thread.
/// </summary>
public sealed class DispatcherIfcTo3dmlConverter : IIfcTo3dmlConverter
{
    private readonly IIfcTo3dmlConverter _inner;
    private readonly Dispatcher _dispatcher;

    public DispatcherIfcTo3dmlConverter(IIfcTo3dmlConverter inner, Dispatcher dispatcher)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task ConvertAsync(
        string ifcPath,
        string outputPath,
        IfcPlacement placement,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _dispatcher
            .InvokeAsync(() => _inner.ConvertAsync(ifcPath, outputPath, placement, progress, cancellationToken))
            .Task
            .Unwrap();
    }
}

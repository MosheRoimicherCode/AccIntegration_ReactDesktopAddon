using System.Windows.Threading;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.Conversion;

/// <summary>
/// Runs TerraExplorer imports on the UI (STA) thread, where the SGWorld COM object lives,
/// even when the conversion finished on a background thread.
/// </summary>
public sealed class DispatcherTerraExplorerImportService : ITerraExplorerImportService
{
    private readonly ITerraExplorerImportService _inner;
    private readonly Dispatcher _dispatcher;

    public DispatcherTerraExplorerImportService(ITerraExplorerImportService inner, Dispatcher dispatcher)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task ImportAsync(
        DownloadedFileMetadata metadata,
        ImportMode importMode,
        CancellationToken cancellationToken = default)
    {
        return _dispatcher
            .InvokeAsync(() => _inner.ImportAsync(metadata, importMode, cancellationToken))
            .Task
            .Unwrap();
    }
}

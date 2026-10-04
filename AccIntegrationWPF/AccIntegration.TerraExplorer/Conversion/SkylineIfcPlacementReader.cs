using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.TerraExplorer.Conversion;

/// <summary>
/// Reads an IFC's pivot and coordinate system with TerraExplorer's 3DML API.
/// Missing values are reported, not thrown: the user fills them in before converting.
/// </summary>
public sealed class SkylineIfcPlacementReader : IIfcPlacementReader
{
    private readonly TerraExplorerConnection _connection;

    public SkylineIfcPlacementReader(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public Task<IfcPlacement> ReadAsync(string ifcPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ifcPath))
            throw new FileNotFoundException("IFC file was not found.", ifcPath);

        cancellationToken.ThrowIfCancellationRequested();
        TerraExplorerAssemblies.EnsureResolvable(_connection.InstallDirectory);

        // Resolve TerraExplorer's installation on its owning thread. The slow native read
        // creates and uses its own 3DML object on a separate STA, keeping WPF responsive.
        return IfcPlacementJob.ReadAsync(ifcPath, cancellationToken);
    }
}

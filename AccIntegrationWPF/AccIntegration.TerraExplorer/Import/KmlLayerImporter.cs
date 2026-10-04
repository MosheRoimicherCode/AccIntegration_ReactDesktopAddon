using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.Import;

public sealed class KmlLayerImporter : ILayerImporter
{
    private readonly TerraExplorerConnection _connection;

    public KmlLayerImporter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".kml", ".kmz" };

    public void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        IKMLLayer81 layer = _connection.Creator.CreateKMLLayer(filePath, string.Empty)
            ?? throw new InvalidOperationException("CreateKMLLayer returned null.");

        layer.Refresh();
        ClientDataValues.ApplyTo(layer, clientData);
    }
}

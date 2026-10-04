using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.Import;

public sealed class MeshLayerImporter : ILayerImporter
{
    private readonly TerraExplorerConnection _connection;

    public MeshLayerImporter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".3dml", ".o3dml" };

    public void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        IMeshLayer81 layer = _connection.Creator.CreateMeshLayerFromFile(filePath, string.Empty)
            ?? throw new InvalidOperationException("CreateMeshLayerFromFile returned null.");

        layer.Refresh();
        ClientDataValues.ApplyTo(layer, clientData);
    }
}

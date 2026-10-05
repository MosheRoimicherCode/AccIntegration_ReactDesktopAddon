using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.Import;

public sealed class FlyLayerImporter : ILayerImporter
{
    private readonly TerraExplorerConnection _connection;

    public FlyLayerImporter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".fly" };

    public void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        IProjectTree81 projectTree = _connection.ProjectTree;

        string objectId = projectTree.LoadFlyLayer(filePath, string.Empty);
        if (string.IsNullOrWhiteSpace(objectId))
            throw new InvalidOperationException("LoadFlyLayer did not return an object id.");

        ITerraExplorerObject81 loadedObject = projectTree.GetObject(objectId)
            ?? throw new InvalidOperationException("LoadFlyLayer object could not be resolved via ProjectTree.GetObject.");

        ClientDataValues.ApplyTo(loadedObject, clientData);
        _connection.World.Navigate.FlyTo(loadedObject);
    }
}

using AccIntegration.Core.Models;
using AccIntegration.TerraExplorer.ClientData;
using TerraExplorerX;

namespace AccIntegration.TerraExplorer.Import;

/// <summary>
/// CAD files loaded as feature layers through TerraExplorer's OGR plug-in.
/// </summary>
public sealed class OgrFeatureLayerImporter : ILayerImporter
{
    private readonly TerraExplorerConnection _connection;

    public OgrFeatureLayerImporter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".rvt", ".dwg", ".dwf", ".dxf" };

    public void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData)
    {
        string connectionString = $"FileName={filePath};TEPlugName=OGR;";

        IFeatureLayer81 layer = _connection.Creator.CreateFeatureLayer(Path.GetFileName(filePath), connectionString, string.Empty)
            ?? throw new InvalidOperationException("CreateFeatureLayer returned null.");

        layer.Refresh();
        ClientDataValues.ApplyTo(layer, clientData);
    }
}

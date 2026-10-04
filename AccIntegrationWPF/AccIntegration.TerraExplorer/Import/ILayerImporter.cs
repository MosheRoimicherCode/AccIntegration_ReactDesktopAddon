using AccIntegration.Core.Models;

namespace AccIntegration.TerraExplorer.Import;

/// <summary>
/// Creates one kind of TerraExplorer layer from a local file.
/// </summary>
public interface ILayerImporter
{
    // Lower-case extensions with the leading dot, e.g. ".3dml".
    IReadOnlyCollection<string> Extensions { get; }

    void Import(string filePath, ImportMode importMode, IReadOnlyDictionary<string, string> clientData);
}

using AccIntegration.Core.Models;

namespace AccIntegration.TerraExplorer.ClientData;

/// <summary>
/// Builds the ClientData written on an imported layer: every metadata property plus the ACC keys (as in TEZ).
/// </summary>
public static class ClientDataBuilder
{
    public const string AccDataKey = "AccData";
    public const string AccHubUrnKey = "AccHubUrn";
    public const string AccProjectUrnKey = "AccProjectUrn";
    public const string AccFileUrnKey = "AccFileUrn";
    public const string AccVersionUrnKey = "AccVersionUrn";
    public const string AccIfcPathKey = "AccIfcPath";

    public static Dictionary<string, string> Build(DownloadedFileMetadata metadata)
    {
        var clientData = new Dictionary<string, string>(metadata.Properties, StringComparer.OrdinalIgnoreCase)
        {
            [AccDataKey] = bool.TrueString,
            [AccProjectUrnKey] = metadata.ProjectUrn,
            [AccFileUrnKey] = metadata.FileUrn,
            [AccVersionUrnKey] = metadata.VersionUrn
        };

        if (!clientData.TryGetValue(AccHubUrnKey, out string? hubUrn) || string.IsNullOrWhiteSpace(hubUrn))
            clientData[AccHubUrnKey] = string.Empty;

        clientData.TryGetValue(AccIfcPathKey, out string? ifcPath);
        if (string.IsNullOrWhiteSpace(ifcPath) &&
            string.Equals(Path.GetExtension(metadata.LocalPath), ".ifc", StringComparison.OrdinalIgnoreCase))
        {
            ifcPath = metadata.LocalPath;
        }

        clientData[AccIfcPathKey] = ifcPath ?? string.Empty;
        return clientData;
    }
}

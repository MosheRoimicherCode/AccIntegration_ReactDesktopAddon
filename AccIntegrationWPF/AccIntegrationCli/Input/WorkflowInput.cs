using AccIntegration.Core.Models;

namespace AccIntegrationCli.Input;

/// <summary>
/// Options shared by the file commands (run/download/import), in the form the ACC web UI sends them.
/// </summary>
public static class WorkflowInput
{
    public const string AccHubUrnPropertyName = "AccHubUrn";
    public const string ImportModePropertyName = "ImportMode";

    /// <summary>
    /// --properties as sent, plus --hub-urn stored as AccHubUrn (written to the layer's ClientData, as in TEZ).
    /// </summary>
    public static Dictionary<string, string> ReadProperties(CliArguments args)
    {
        Dictionary<string, string> properties = PropertiesParser.Parse(args.GetOptional("properties"));

        string? hubUrn = args.GetOptional("hub-urn", "acc-hub-urn");
        if (!string.IsNullOrWhiteSpace(hubUrn))
            properties[AccHubUrnPropertyName] = hubUrn;

        return properties;
    }

    /// <summary>
    /// --import-mode, else the ImportMode property (where the web UI puts the TIFF choice).
    /// </summary>
    public static ImportMode ReadImportMode(CliArguments args, IReadOnlyDictionary<string, string> properties)
    {
        string? mode = args.GetOptional("import-mode");
        if (string.IsNullOrWhiteSpace(mode))
            properties.TryGetValue(ImportModePropertyName, out mode);

        return ImportModeParser.Parse(mode);
    }
}

using AccIntegration.Core.Models;

namespace AccIntegrationCli.Input;

/// <summary>
/// Parses --import-mode. Accepts TEZ values (image_layer, elevation_layer) and enum names (ImageLayer).
/// </summary>
public static class ImportModeParser
{
    public static ImportMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ImportMode.Default;

        string normalized = value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "default" => ImportMode.Default,
            "imagelayer" => ImportMode.ImageLayer,
            "elevationlayer" => ImportMode.ElevationLayer,
            _ => throw new ArgumentException(
                $"Invalid --import-mode '{value}'. Expected image_layer or elevation_layer.")
        };
    }
}

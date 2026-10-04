using System.Globalization;

namespace AccIntegration.TerraExplorer.ClientData;

/// <summary>
/// Reads import settings (raster bounds, GroupID, Description...) from ClientData, and writes ClientData on layers.
/// </summary>
public static class ClientDataValues
{
    public static double GetDouble(IReadOnlyDictionary<string, string> clientData, string key, double defaultValue)
    {
        if (!clientData.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (double.TryParse(value, out double parsed))
            return parsed;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return defaultValue;
    }

    public static string GetString(IReadOnlyDictionary<string, string> clientData, string key, string defaultValue)
    {
        return clientData.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    /// <summary>
    /// Calls SetClientData(key, value) on any TerraExplorer object that supports it.
    /// </summary>
    public static void ApplyTo(dynamic terraObject, IReadOnlyDictionary<string, string> clientData)
    {
        foreach ((string key, string value) in clientData)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            terraObject.SetClientData(key, value ?? string.Empty);
        }
    }
}

using System.Text.Json;

namespace AccIntegrationCli.Input;

/// <summary>
/// Parses the --properties value: a JSON object, or the loose format {Key: Value, Key2=Value2}.
/// </summary>
public static class PropertiesParser
{
    public static Dictionary<string, string> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string trimmed = input.Trim();

        try
        {
            using JsonDocument json = JsonDocument.Parse(trimmed);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("--properties must be a JSON object.");

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in json.RootElement.EnumerateObject())
            {
                result[property.Name] = JsonValueToString(property.Value);
            }

            return result;
        }
        catch (JsonException)
        {
            return ParseLoose(trimmed);
        }
    }

    private static Dictionary<string, string> ParseLoose(string input)
    {
        string working = input;
        if (working.StartsWith('{') && working.EndsWith('}') && working.Length >= 2)
            working = working[1..^1];

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(working))
            return result;

        foreach (string pair in working.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = pair.IndexOf(':');
            if (separatorIndex < 0)
                separatorIndex = pair.IndexOf('=');

            if (separatorIndex <= 0 || separatorIndex == pair.Length - 1)
                throw new FormatException($"Invalid --properties token '{pair}'. Expected key:value or key=value.");

            string key = TrimQuotes(pair[..separatorIndex]);
            if (string.IsNullOrWhiteSpace(key))
                throw new FormatException($"Invalid key in --properties token '{pair}'.");

            result[key] = TrimQuotes(pair[(separatorIndex + 1)..]);
        }

        return result;
    }

    private static string JsonValueToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static string TrimQuotes(string token)
    {
        string value = token.Trim();
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1].Trim();
        }

        return value;
    }
}

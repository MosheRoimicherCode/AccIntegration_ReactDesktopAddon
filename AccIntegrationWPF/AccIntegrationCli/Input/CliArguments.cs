using System.Text;

namespace AccIntegrationCli.Input;

/// <summary>
/// Parsed command line: a command name followed by '--name value' or '--name=value' options.
/// </summary>
public sealed class CliArguments
{
    private readonly Dictionary<string, string> _options;

    private CliArguments(string command, Dictionary<string, string> options)
    {
        Command = command;
        _options = options;
    }

    public string Command { get; }

    public static CliArguments Parse(string[] args)
    {
        string command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Invalid argument '{token}'. Expected '--name value' format.");

            string option = token[2..];
            string name;
            string value;

            int separatorIndex = option.IndexOf('=');
            if (separatorIndex >= 0)
            {
                name = option[..separatorIndex].Trim();
                value = option[(separatorIndex + 1)..].Trim();
            }
            else
            {
                name = option.Trim();
                // A flag without a value counts as true.
                value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[++i]
                    : bool.TrueString;
            }

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"Invalid option name in '{token}'.");

            options[name] = value;
        }

        return new CliArguments(command, options);
    }

    public string GetRequired(params string[] names)
    {
        foreach (string name in names)
        {
            if (!_options.TryGetValue(name, out string? value))
                continue;

            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            return value.Trim();
        }

        throw new ArgumentException($"Missing required option '--{string.Join("' or '--", names)}'.");
    }

    public string? GetOptional(params string[] names)
    {
        foreach (string name in names)
        {
            if (_options.TryGetValue(name, out string? value))
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Reads a boolean option (true/false, 1/0, yes/no, y/n, on/off). A bare flag counts as true.
    /// </summary>
    public bool GetBool(bool defaultValue, params string[] names)
    {
        foreach (string name in names)
        {
            if (!_options.TryGetValue(name, out string? value))
                continue;

            return value.Trim().ToLowerInvariant() switch
            {
                "" or "1" or "true" or "yes" or "y" or "on" => true,
                "0" or "false" or "no" or "n" or "off" => false,
                _ => throw new ArgumentException($"Option '--{name}' expects true or false. Received '{value}'.")
            };
        }

        return defaultValue;
    }

    public void EnsureNoOptions()
    {
        if (_options.Count > 0)
            throw new ArgumentException($"The '{Command}' command does not accept options.");
    }

    /// <summary>
    /// Formats raw arguments as one line for the log file.
    /// </summary>
    public static string FormatForLog(string[] args)
    {
        if (args.Length == 0)
            return "<no arguments>";

        var builder = new StringBuilder();
        foreach (string argument in args)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            bool requiresQuotes = argument.Length == 0 || argument.IndexOfAny(new[] { ' ', '"', '\t' }) >= 0;
            builder.Append(requiresQuotes ? "\"" + argument.Replace("\"", "\\\"") + "\"" : argument);
        }

        return builder.ToString();
    }
}

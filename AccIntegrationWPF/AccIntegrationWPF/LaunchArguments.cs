using System.IO;

namespace AccIntegrationWPF;

/// <summary>
/// Parameters the CLI passes when it hands an IFC over: --ifc "&lt;path&gt;" --output "&lt;path&gt;".
/// </summary>
public sealed class LaunchArguments
{
    public string? IfcPath { get; private init; }

    public string? OutputPath { get; private init; }

    public string? CompletionPipeName { get; private init; }

    // Why the arguments cannot be used, or null. No arguments at all is not an error (opened by hand).
    public string? Error { get; private init; }

    public bool HasConversionRequest =>
        Error is null && !string.IsNullOrWhiteSpace(IfcPath) && !string.IsNullOrWhiteSpace(OutputPath);

    public static LaunchArguments Parse(string[] args)
    {
        string? ifcPath = null;
        string? outputPath = null;
        string? completionPipeName = null;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--ifc", StringComparison.OrdinalIgnoreCase))
                ifcPath = args[++i];
            else if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase))
                outputPath = args[++i];
            else if (string.Equals(args[i], "--completion-pipe", StringComparison.OrdinalIgnoreCase))
                completionPipeName = args[++i];
        }

        return new LaunchArguments
        {
            IfcPath = ifcPath,
            OutputPath = outputPath,
            CompletionPipeName = completionPipeName,
            Error = Validate(args, ifcPath, outputPath)
        };
    }

    private static string? Validate(string[] args, string? ifcPath, string? outputPath)
    {
        if (args.Length == 0)
            return null;

        if (string.IsNullOrWhiteSpace(ifcPath) || string.IsNullOrWhiteSpace(outputPath))
            return $"Invalid launch arguments '{string.Join(' ', args)}'. Expected --ifc \"<path>\" --output \"<path>\".";

        if (!File.Exists(ifcPath))
            return $"The IFC file to convert was not found: '{ifcPath}'.";

        if (!string.Equals(Path.GetExtension(ifcPath), ".ifc", StringComparison.OrdinalIgnoreCase))
            return $"Only IFC files can be sent to the local processor: '{ifcPath}'.";

        return null;
    }
}

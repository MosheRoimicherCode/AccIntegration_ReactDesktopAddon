using System.Diagnostics;
using AccIntegration.Core.Interfaces;

namespace AccIntegrationCli.Launchers;

/// <summary>
/// Starts the WPF local processor with: --ifc "&lt;ifcPath&gt;" --output "&lt;outputPath&gt;".
/// </summary>
public sealed class WpfConversionLauncher : IIfcConversionLauncher
{
    public const string WpfExeName = "AccIntegrationWPF.exe";

    private readonly string _wpfExePath;

    public WpfConversionLauncher(string? wpfExePath = null)
    {
        // Published side by side with the CLI in the add-on folder.
        _wpfExePath = wpfExePath ?? Path.Combine(AppContext.BaseDirectory, WpfExeName);
    }

    public void Launch(string ifcPath, string outputPath)
    {
        if (!File.Exists(_wpfExePath))
            throw new FileNotFoundException("The local processor (WPF) was not found.", _wpfExePath);

        // Shell execute: the WPF process must not inherit the CLI's stdout/stderr pipes,
        // otherwise the caller would wait for the WPF window to close before seeing the CLI end.
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = _wpfExePath,
            Arguments = $"--ifc {Quote(ifcPath)} --output {Quote(outputPath)}",
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_wpfExePath) ?? string.Empty
        });

        if (process is null)
            throw new InvalidOperationException($"Failed to start the local processor '{_wpfExePath}'.");
    }

    private static string Quote(string value)
    {
        if (value.Contains('"'))
            throw new ArgumentException($"Path contains a quote character: '{value}'.");

        return $"\"{value}\"";
    }
}

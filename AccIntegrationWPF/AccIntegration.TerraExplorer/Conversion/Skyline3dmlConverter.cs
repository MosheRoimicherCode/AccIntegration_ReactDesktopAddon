using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.TerraExplorer.Conversion;

/// <summary>
/// Converts IFC to 3DML in-process with TerraExplorer's 3DML API, at the given placement. Writes to name.partial.3dml
/// and renames on success, so a failed or cancelled run never leaves a usable-looking 3DML.
/// Requires TerraExplorer 8.1 running (the 3DML API checks its license).
/// </summary>
public sealed class Skyline3dmlConverter : IIfcTo3dmlConverter
{
    private readonly TerraExplorerConnection _connection;

    public Skyline3dmlConverter(TerraExplorerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task ConvertAsync(
        string ifcPath,
        string outputPath,
        IfcPlacement placement,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ifcPath))
            throw new FileNotFoundException("IFC file was not found.", ifcPath);

        if (!string.Equals(Path.GetExtension(outputPath), ".3dml", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The output must be a .3dml file: '{outputPath}'.", nameof(outputPath));

        if (File.Exists(outputPath))
            throw new IOException($"The output 3DML already exists: '{outputPath}'.");

        TerraExplorerAssemblies.EnsureResolvable(_connection.InstallDirectory);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string partialPath = Path.ChangeExtension(outputPath, ".partial.3dml");
        TryDelete(partialPath);

        try
        {
            await BimCreationJob.RunAsync(ifcPath, partialPath, placement, progress, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(partialPath))
                throw new InvalidOperationException($"3DML creation finished but produced no file: '{partialPath}'.");

            File.Move(partialPath, outputPath);
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup failures; the original error is what matters.
        }
    }
}

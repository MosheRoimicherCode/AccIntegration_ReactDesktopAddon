using System.Runtime.Loader;

namespace AccIntegration.TerraExplorer.Conversion;

/// <summary>
/// Lets .NET load TerraExplorer's managed assemblies (3DML.Api and dependencies) and their native DLLs
/// from the TerraExplorer install folder. Must run before any 3DML.Api type is touched.
/// </summary>
internal static class TerraExplorerAssemblies
{
    private static readonly object Sync = new();
    private static string? _registeredDirectory;

    public static void EnsureResolvable(string installDirectory)
    {
        lock (Sync)
        {
            if (_registeredDirectory is not null)
                return;

            if (!File.Exists(Path.Combine(installDirectory, "3DML.Api.dll")))
                throw new FileNotFoundException("3DML.Api.dll was not found in the TerraExplorer folder.", installDirectory);

            // Native dependencies (GDAL, SQLite...) are found through PATH.
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", installDirectory + Path.PathSeparator + path);

            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                string file = Path.Combine(installDirectory, name.Name + ".dll");
                return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
            };

            _registeredDirectory = installDirectory;
        }
    }
}

using TerraExplorerX;

namespace AccIntegration.TerraExplorer;

/// <summary>
/// Lazily attaches to the running TerraExplorer through the SGWorld81 COM object.
/// Nothing touches COM until a member is first used.
/// </summary>
public sealed class TerraExplorerConnection
{
    private SGWorld81? _world;

    public SGWorld81 World => _world ??= new SGWorld81();

    public ICreator81 Creator =>
        World.Creator ?? throw new InvalidOperationException("TerraExplorer Creator is not available.");

    public IProjectTree81 ProjectTree =>
        World.ProjectTree ?? throw new InvalidOperationException("TerraExplorer ProjectTree is not available.");

    public ICoordServices81 CoordServices =>
        World.CoordServices ?? throw new InvalidOperationException("TerraExplorer CoordServices is not available.");

    // Folder of the running TerraExplorer (holds 3DML.Api.dll and its native dependencies).
    public string InstallDirectory =>
        Path.GetDirectoryName(World.Application.ExecutablePath)
        ?? throw new InvalidOperationException("TerraExplorer install folder could not be resolved.");
}

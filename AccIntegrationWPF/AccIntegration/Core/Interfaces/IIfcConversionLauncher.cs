namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Hands an IFC over to the local processor (WPF UI) that converts it to 3DML.
/// Fire-and-forget: nothing is reported back to the caller.
/// </summary>
public interface IIfcConversionLauncher
{
    void Launch(string ifcPath, string outputPath);
}

namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Opens a local file with the default application registered for its type.
/// </summary>
public interface IOpenFileService
{
    /// <summary>
    /// Returns true when the open request was handed to the default application.
    /// </summary>
    bool Open(string filePath);
}

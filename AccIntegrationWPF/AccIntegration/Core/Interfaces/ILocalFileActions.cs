namespace AccIntegration.Core.Interfaces;

/// <summary>
/// Actions on stored files in Windows: show a file in Explorer, delete it from disk.
/// </summary>
public interface ILocalFileActions
{
    /// <summary>
    /// Opens Windows Explorer with the file selected. Returns true when Explorer was started.
    /// </summary>
    bool ShowInFolder(string filePath);

    /// <summary>
    /// Deletes the file from disk. Returns false when it did not exist.
    /// </summary>
    bool Delete(string filePath);
}

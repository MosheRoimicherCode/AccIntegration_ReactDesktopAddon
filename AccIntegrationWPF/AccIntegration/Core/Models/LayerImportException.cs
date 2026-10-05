namespace AccIntegration.Core.Models;

/// <summary>
/// Adds the local file path to an import failure so CLI callers can report it.
/// </summary>
public sealed class LayerImportException : Exception
{
    public LayerImportException(string filePath, Exception innerException)
        : base(innerException.Message, innerException)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}

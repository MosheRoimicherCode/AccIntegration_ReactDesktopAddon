namespace AccIntegration.Core.Models;

/// <summary>
/// How the UI asked for a file to be imported. Required for TIFF; ignored for other types.
/// </summary>
public enum ImportMode
{
    Default = 0,

    ImageLayer,

    ElevationLayer
}

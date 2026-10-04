namespace AccIntegration.Core.Models;

public sealed class DownloadedFileStorageOptions
{
    private string VendorFolderName { get; init; } = "Skyline";
    private string SoftwareFolderName { get; init; } = "TerraExplorer";
    private string AddonsFolderName { get; init; } = "Add-ons";
    private string MainFolderName { get; init; } = "AccIntegration";
    private string DownloadedFilesFolderName { get; init; } = "Downloaded Files";
    private string MetadataDatabaseFolderName { get; init; } = "downloaded-files-db-shards";

    public string ResolveAppFolderPath()
    {
        string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(roamingAppData, VendorFolderName, SoftwareFolderName, AddonsFolderName, MainFolderName);
    }

    public string ResolveDownloadedFilesFolderPath()
    {
        return Path.Combine(ResolveAppFolderPath(), DownloadedFilesFolderName);
    }

    public string ResolveMetadataDatabaseFolder()
    {
        return Path.Combine(ResolveDownloadedFilesFolderPath(), MetadataDatabaseFolderName);
    }
}

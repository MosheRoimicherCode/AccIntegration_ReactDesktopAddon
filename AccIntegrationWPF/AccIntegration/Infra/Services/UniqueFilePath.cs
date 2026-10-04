namespace AccIntegration.Infra.Services;

internal static class UniqueFilePath
{
    /// <summary>
    /// Returns folder\fileName, or folder\name_1.ext, name_2.ext... when the name is taken.
    /// </summary>
    public static string Build(string folderPath, string fileName)
    {
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        string destinationPath = Path.Combine(folderPath, fileName);
        int counter = 1;

        while (File.Exists(destinationPath))
        {
            destinationPath = Path.Combine(folderPath, $"{nameWithoutExtension}_{counter}{extension}");
            counter++;
        }

        return destinationPath;
    }
}

namespace AccIntegration.Core.Models;

/// <summary>
/// Maps file extensions to the operation that handles them.
/// </summary>
public static class FileOperationMap
{
    public static readonly IReadOnlyDictionary<string, FileOperation> Extensions =
        new Dictionary<string, FileOperation>(StringComparer.OrdinalIgnoreCase)
        {
            [".ifc"] = FileOperation.ConvertIfcTo3dml,

            [".3dml"] = FileOperation.ImportToTerraExplorer,
            [".o3dml"] = FileOperation.ImportToTerraExplorer,
            [".rvt"] = FileOperation.ImportToTerraExplorer,
            [".dwg"] = FileOperation.ImportToTerraExplorer,
            [".dwf"] = FileOperation.ImportToTerraExplorer,
            [".dxf"] = FileOperation.ImportToTerraExplorer,
            [".shp"] = FileOperation.ImportToTerraExplorer,
            [".kml"] = FileOperation.ImportToTerraExplorer,
            [".kmz"] = FileOperation.ImportToTerraExplorer,
            [".ecw"] = FileOperation.ImportToTerraExplorer,
            [".tif"] = FileOperation.ImportToTerraExplorer,
            [".tiff"] = FileOperation.ImportToTerraExplorer,
            [".fly"] = FileOperation.ImportToTerraExplorer,

            [".pdf"] = FileOperation.OpenWithDefaultApp,
            [".xls"] = FileOperation.OpenWithDefaultApp,
            [".xlsx"] = FileOperation.OpenWithDefaultApp,
            [".txt"] = FileOperation.OpenWithDefaultApp,
            [".csv"] = FileOperation.OpenWithDefaultApp,
            [".zip"] = FileOperation.OpenWithDefaultApp,

            [".shx"] = FileOperation.StoreOnly,
            [".dbf"] = FileOperation.StoreOnly,
            [".prj"] = FileOperation.StoreOnly,
            [".cpg"] = FileOperation.StoreOnly,
            [".qix"] = FileOperation.StoreOnly,
            [".sbn"] = FileOperation.StoreOnly,
            [".sbx"] = FileOperation.StoreOnly
        };

    /// <summary>
    /// Resolves the operation from a file path (C:\a\model.ifc), an extension (.ifc)
    /// or a bare extension as stored in metadata (IFC). Unmapped values return Unsupported.
    /// </summary>
    public static FileOperation Resolve(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
            return FileOperation.Unsupported;

        string value = pathOrExtension.Trim();
        string extension = value.Contains('.') ? Path.GetExtension(value) : $".{value}";

        return Extensions.TryGetValue(extension, out FileOperation operation)
            ? operation
            : FileOperation.Unsupported;
    }
}

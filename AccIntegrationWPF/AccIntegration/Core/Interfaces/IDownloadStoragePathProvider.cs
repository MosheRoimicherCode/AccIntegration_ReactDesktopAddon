using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccIntegration.Core.Interfaces
{
    public interface IDownloadStoragePathProvider
    {
        string AppFolderPath { get; }

        string DownloadedFilesRootPath { get; }

        string MetadataDatabaseFolderPath { get; }

        string EnsureFolderForExtension(string? extension);
    }
}

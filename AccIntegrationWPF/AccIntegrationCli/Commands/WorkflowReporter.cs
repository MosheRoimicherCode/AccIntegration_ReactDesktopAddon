using AccIntegration.Core.Models;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Start/end events shared by the file commands (run, download, import).
/// </summary>
public sealed class WorkflowReporter
{
    private readonly EventWriter _events;

    public WorkflowReporter(EventWriter events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Started(Dictionary<string, object?> data)
    {
        _events.Info("workflow.started", "Workflow started.", data);
    }

    public void Completed(FileOperationResult result)
    {
        if (result.Operation == FileOperation.ConvertIfcTo3dml)
        {
            // The web UI shows this as a popup; the WPF local processor continues on its own.
            _events.Info(
                "local_processor.started",
                "Local processor started. Please continue in TerraExplorer main UI.",
                new Dictionary<string, object?>
                {
                    ["ifcPath"] = result.File.FilePath,
                    ["outputPath"] = result.OutputPath
                });
        }

        _events.Info(
            "workflow.completed",
            "Workflow completed successfully.",
            new Dictionary<string, object?>
            {
                ["operation"] = result.Operation,
                ["importMode"] = result.ImportMode,
                ["filePath"] = result.File.FilePath,
                ["localPath"] = result.File.FilePath,
                ["outputPath"] = result.OutputPath,
                ["usedLocalFile"] = result.File.UsedLocalFile,
                ["downloaded"] = result.File.Downloaded,
                ["is3dml"] = result.File.Is3dml,
                ["metadataId"] = result.File.Metadata.Id
            });
    }
}

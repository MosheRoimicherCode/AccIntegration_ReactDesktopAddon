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

    public bool Completed(FileOperationResult result)
    {
        bool success = result.Operation != FileOperation.ConvertIfcTo3dml ||
                       result.IfcConversionResult?.Success == true;
        string? failureMessage = result.IfcConversionResult?.Message;
        string importStatus = success ? "uploaded" : result.IfcConversionResult?.Status ?? "failed";

        if (success && (result.Operation is FileOperation.ImportToTerraExplorer or FileOperation.ConvertIfcTo3dml))
        {
            string importedPath = result.Operation == FileOperation.ConvertIfcTo3dml
                ? result.OutputPath ?? string.Empty
                : result.File.FilePath;
            var layerData = new Dictionary<string, object?>
            {
                ["fileName"] = Path.GetFileName(importedPath),
                ["layerName"] = Path.GetFileNameWithoutExtension(importedPath),
                ["filePath"] = importedPath,
                ["extension"] = Path.GetExtension(importedPath)
            };
            if (result.Operation == FileOperation.ConvertIfcTo3dml)
            {
                layerData["ifcPath"] = result.File.FilePath;
                layerData["outputPath"] = result.OutputPath;
            }

            _events.Info("layer.imported", "Layer imported into TerraExplorer.", layerData);
        }

        var workflowData = new Dictionary<string, object?>
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
        };

        if (success)
            _events.Info("workflow.completed", "Workflow completed successfully.", workflowData);
        else
            _events.Error("workflow.failed", failureMessage ?? "IFC conversion or import failed.", workflowData,
                result.IfcConversionResult?.FailureException);

        if (result.Operation == FileOperation.ImportToTerraExplorer || result.Operation == FileOperation.ConvertIfcTo3dml)
        {
            string filePath = result.Operation == FileOperation.ConvertIfcTo3dml
                ? result.OutputPath ?? string.Empty
                : result.File.FilePath;
            string extension = Path.GetExtension(filePath);
            string layerName = Path.GetFileNameWithoutExtension(filePath);
            var data = new Dictionary<string, object?>
            {
                ["path"] = filePath,
                ["extension"] = extension,
                ["layerName"] = layerName
            };
            if (result.Operation == FileOperation.ConvertIfcTo3dml)
            {
                data["ifcPath"] = result.File.FilePath;
                data["outputPath"] = result.OutputPath;
            }

            var payload = new Dictionary<string, object?>
            {
                ["success"] = success,
                ["status"] = importStatus,
                ["filePath"] = filePath,
                ["layerName"] = layerName,
                ["extension"] = extension,
                ["message"] = success ? null : failureMessage
            };
            if (result.Operation == FileOperation.ConvertIfcTo3dml)
            {
                payload["ifcPath"] = result.File.FilePath;
                payload["outputPath"] = result.OutputPath;
            }

            _events.ImportResult(
                success,
                importStatus,
                success ? "Layer imported into TerraExplorer." : failureMessage ?? "IFC conversion or import failed.",
                data,
                payload);
        }

        return success;
    }

    public void FailedLayerImport(string? filePath, Exception exception)
    {
        string extension = Path.GetExtension(filePath ?? string.Empty);
        string layerName = Path.GetFileNameWithoutExtension(filePath ?? string.Empty);
        string message = exception is OperationCanceledException ? "Cancelled by user." : exception.Message;
        string status = exception is OperationCanceledException ? "cancelled" : "failed";

        _events.ImportResult(
            success: false,
            status,
            message,
            new Dictionary<string, object?>
            {
                ["path"] = filePath,
                ["extension"] = extension,
                ["layerName"] = layerName
            },
            new Dictionary<string, object?>
            {
                ["success"] = false,
                ["status"] = status,
                ["filePath"] = filePath,
                ["layerName"] = layerName,
                ["extension"] = extension,
                ["message"] = message
            }, exception);
    }
}

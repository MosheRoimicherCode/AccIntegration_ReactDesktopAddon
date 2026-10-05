using System.IO;
using System.Windows.Threading;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegration.Infra.Logging;
using AccIntegrationWPF;
using AccIntegrationWPF.Bridge;
using AccIntegrationWPF.Conversion;

string root = Path.Combine(Path.GetTempPath(), "AccIntegration-WpfLoggingAudit", Guid.NewGuid().ToString("N"));
int passed = 0, failed = 0;
await Check("UI error is persisted before WebView is ready", f =>
{
    var bridge = new WebViewBridge(Dispatcher.CurrentDispatcher, f.Logger);
    bridge.SendError("ui-marker");
    Require(f.Log.Contains("ui-marker") && f.Log.Contains("source=WPF") && f.Log.Contains("pid="), "UI error/context missing.");
    return Task.CompletedTask;
});
await Check("UI exception retains full diagnostic information once", f =>
{
    var bridge = new WebViewBridge(Dispatcher.CurrentDispatcher, f.Logger);
    try { throw new InvalidOperationException("outer-marker", new IOException("inner-marker")); }
    catch (Exception ex) { bridge.SendError(ex, "Readable UI message"); }
    Require(f.Log.Contains("outer-marker") && f.Log.Contains("inner-marker") && f.Log.Contains("Program.cs"), "Exception diagnostics lost.");
    Require(f.Log.Split("event=wpf.ui.error").Length == 2, "Exception logged more than once.");
    return Task.CompletedTask;
});
await Check("WPF exception URLs are redacted in file logs", f =>
{
    var bridge = new WebViewBridge(Dispatcher.CurrentDispatcher, f.Logger);
    bridge.SendError(new IOException("Download failed: https://example.invalid/model?sig=WPF_SECRET"));
    Require(f.Log.Contains("example.invalid/model") && !f.Log.Contains("WPF_SECRET"), "WPF URL credentials leaked.");
    return Task.CompletedTask;
});
await Check("Bridge handles a failed logger without recursive logging", f =>
{
    string blocked = Path.Combine(f.Root, "blocked");
    File.WriteAllText(blocked, "fixture");
    using var logger = new FileLogger(blocked, "WPF");
    var bridge = new WebViewBridge(Dispatcher.CurrentDispatcher, logger);
    int notifications = 0;
    logger.WriteFailed += _ => notifications++;
    using var stderr = new StringWriter();
    var previous = Console.Error;
    try { Console.SetError(stderr); bridge.SendError("original-error"); }
    finally { Console.SetError(previous); }
    Require(notifications == 1 && logger.LastWriteFailure is not null, "Failure notification missing or recursive.");
    Require(stderr.ToString().Contains("logging.failed"), "Fallback diagnostic missing.");
    return Task.CompletedTask;
});
await Check("Standalone WPF preparation failure is logged without CLI", async f =>
{
    var orchestrator = new ControlledOrchestrator { FailPreparation = true };
    var queue = new ConversionQueue(orchestrator, f.Logger);
    var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    queue.JobCompleted += (_, _) => finished.TrySetResult();
    queue.Enqueue(f.Request());
    queue.Start();
    await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Require(f.Log.Contains("event=wpf.import.failed") && f.Log.Contains("prepare-marker") && f.Log.Contains("inner-marker"), "Preparation failure missing.");
    Require(!f.Log.Contains("status=uploaded"), "False success logged.");
});
foreach (bool failImport in new[] { false, true })
{
    await Check(failImport ? "Conversion/import failure is logged" : "Success is logged only after import completes", async f =>
    {
        var orchestrator = new ControlledOrchestrator();
        var queue = new ConversionQueue(orchestrator, f.Logger);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.JobReady += (_, placement) => queue.Submit(placement);
        queue.JobCompleted += (_, _) => finished.TrySetResult();
        queue.Enqueue(f.Request());
        queue.Start();
        await orchestrator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(!f.Log.Contains("event=wpf.import.completed"), "Success logged before import finished.");
        if (failImport)
            orchestrator.Completion.SetException(new IOException("import-marker"));
        else
            orchestrator.Completion.SetResult(new DownloadedFileMetadata());
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(f.Log.Contains(failImport ? "status=failed" : "status=uploaded") && f.Log.Contains("model.ifc") && f.Log.Contains("model.3dml"), "Final outcome/paths missing.");
        if (failImport) Require(f.Log.Contains("import-marker") && !f.Log.Contains("status=uploaded"), "Import failure diagnostics wrong.");
    });
}
await Check("User cancellation is recorded as an informational outcome", async f =>
{
    var queue = new ConversionQueue(new ControlledOrchestrator(), f.Logger);
    var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    queue.JobReady += (_, _) => queue.CancelCurrent();
    queue.JobCompleted += (_, _) => finished.TrySetResult();
    queue.Enqueue(f.Request());
    queue.Start();
    await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Require(f.Log.Contains("status=cancelled") && !f.Log.Contains("[ERROR]") && !f.Log.Contains("status=uploaded"), "Cancellation misclassified.");
});
Console.WriteLine($"RESULT: {passed} passed, {failed} failed. Evidence: {root}");
return failed == 0 ? 0 : 1;

async Task Check(string name, Func<Fixture, Task> check)
{
    using var fixture = new Fixture(Path.Combine(root, (passed + failed + 1).ToString("D2")));
    try { await check(fixture); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex}"); }
}
static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

sealed class Fixture : IDisposable
{
    public string Root { get; }
    public FileLogger Logger { get; }
    public Fixture(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
        Logger = new FileLogger(root, "WPF");
    }
    public string Log => string.Join("\n", Directory.GetFiles(Root, "*.log", SearchOption.AllDirectories).Select(File.ReadAllText));
    public LaunchArguments Request()
    {
        string input = Path.Combine(Root, "model.ifc");
        File.WriteAllText(input, "test fixture");
        return LaunchArguments.Parse(["--ifc", input, "--output", Path.Combine(Root, "model.3dml")]);
    }
    public void Dispose() => Logger.Dispose();
}
sealed class ControlledOrchestrator : IIfcConversionOrchestrator
{
    public bool FailPreparation { get; init; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<DownloadedFileMetadata> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<IfcPlacement> PrepareAsync(string path, CancellationToken token = default) =>
        FailPreparation
            ? Task.FromException<IfcPlacement>(new InvalidOperationException("prepare-marker", new IOException("inner-marker")))
            : Task.FromResult(new IfcPlacement());
    public Task<DownloadedFileMetadata> RunAsync(string input, string output, IfcPlacement placement,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        return Completion.Task.WaitAsync(cancellationToken);
    }
}

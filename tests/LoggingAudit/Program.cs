using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegration.Infra.Logging;
using AccIntegrationCli;
using AccIntegrationCli.Commands;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;
using System.Text.Json;

if (args is ["--log-worker", var workerRoot, var workerId])
    return LoggingConcurrencyChecks.RunWorker(workerRoot, workerId);

// Each check uses a unique temporary directory and real file writes.
// Checks use production logging/reporting code and isolated fixtures.
var root = Path.Combine(Path.GetTempPath(), "AccIntegration-LoggingAudit", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0, failed = 0;

await Check("Structured error is flushed to disk and stderr", async f =>
{
    f.Events.Error("test.failed", "error-marker");
    Require(f.Log.Contains("[ERROR]") && f.Log.Contains("error-marker"), "Error absent from disk.");
    Require(f.Stderr.ToString().Contains("test.failed"), "Error absent from stderr.");
    await Task.CompletedTask;
});
await Check("Unknown command is logged", async f =>
{
    Require(await f.App().RunAsync(["missing-command"], default) == 1, "Wrong exit code.");
    Require(f.Log.Contains("event=command.unknown"), "Unknown command not logged.");
});
await Check("Argument parsing error is logged", async f =>
{
    Require(await f.App().RunAsync(["probe", "invalid-positional"], default) == 1, "Wrong exit code.");
    Require(f.Log.Contains("event=command.failed") && f.Log.Contains("ArgumentException"), "Parse failure not logged.");
});
await Check("License failure is logged", async f =>
{
    var app = f.App(new ProbeCommand(true), new LicenseStub(true));
    Require(await app.RunAsync(["probe"], default) == 1, "Wrong exit code.");
    Require(f.Log.Contains("event=license.validation.failed") && f.Log.Contains("license-marker"), "License failure absent.");
});
await Check("Unhandled command exception is logged", async f =>
{
    Require(await f.App(new ProbeCommand(false, true)).RunAsync(["probe"], default) == 1, "Wrong exit code.");
    Require(f.Log.Contains("event=command.failed") && f.Log.Contains("outer-marker"), "Command failure absent.");
});
await Check("Command log retains stack and inner exception", async f =>
{
    await f.App(new ProbeCommand(false, true)).RunAsync(["probe"], default);
    Require(f.Log.Contains("inner-marker") && f.Log.Contains("ProbeCommand.ExecuteAsync"), "Only outer message/type recorded; stack and inner exception lost.");
});
await Check("Direct import failure is logged", f =>
{
    new WorkflowReporter(f.Events).FailedLayerImport("model.3dml", new IOException("import-marker"));
    Require(f.Log.Contains("event=terra.import.failed") && f.Log.Contains("import-marker"), "Import failure absent.");
    return Task.CompletedTask;
});
foreach (string status in new[] { "failed", "cancelled" })
{
    await Check($"IFC {status} outcome is logged without success", f =>
    {
        bool success = new WorkflowReporter(f.Events).Completed(new FileOperationResult
        {
            Operation = FileOperation.ConvertIfcTo3dml,
            File = new LocalFileResult { FilePath = "model.ifc" }, OutputPath = "model.3dml",
            IfcConversionResult = new IfcConversionResult { Success = false, Status = status, Message = "ifc-marker" }
        });
        Require(!success && f.Log.Contains("ifc-marker") && f.Log.Contains("event=terra.import.failed"), "IFC failure absent.");
        Require(!f.Log.Contains("event=terra.import.completed"), "False success logged.");
        return Task.CompletedTask;
    });
}
await Check("Import-result log preserves machine-readable cancellation status", f =>
{
    new WorkflowReporter(f.Events).FailedLayerImport("model.ifc", new OperationCanceledException());
    Require(f.Stderr.ToString().Contains("\"status\":\"cancelled\""), "Fixture did not emit cancelled status.");
    Require(f.Log.Contains("\"status\":\"cancelled\""), "Status exists on stderr but is omitted from file log.");
    return Task.CompletedTask;
});
await Check("License query failure is documented", async f =>
{
    var command = new AccExtensionCommand(new LicenseStub(true), f.Events);
    Require(await f.App(command).RunAsync(["acc-extension"], default) == 1, "Wrong exit code.");
    Require(f.Log.Contains("license-marker"), "License query failure absent.");
});
await Check("Overlapping logger instances preserve every entry", f =>
{
    using var second = new FileLogger(f.Root);
    f.Logger.Error("instance-A-first");
    second.Error("instance-B-first");
    f.Logger.Error("instance-A-second");
    second.Error("instance-B-second");
    foreach (string marker in new[] { "instance-A-first", "instance-B-first", "instance-A-second", "instance-B-second" })
        Require(f.Log.Contains(marker), $"Lost log entry: {marker}");
    return Task.CompletedTask;
});
await Check("Separate processes preserve every complete entry", f => LoggingConcurrencyChecks.VerifyAsync(f.Root, false));
await Check("Separate processes coordinate size rotation", f => LoggingConcurrencyChecks.VerifyAsync(f.Root, true));
await Check("Logger recovers when a mutex owner exits", f =>
{
    const string mutexName = @"Local\AccIntegration.SharedLog";
    // Keep the named object alive after its owning thread exits without releasing it.
    using var keepAlive = new Mutex(false, mutexName);
    Exception? ownerFailure = null;
    var owner = new Thread(() =>
    {
        try
        {
            using var mutex = new Mutex(false, mutexName);
            if (!mutex.WaitOne(TimeSpan.FromSeconds(5)))
                throw new Exception("Could not acquire mutex for abandonment fixture.");
        }
        catch (Exception ex) { ownerFailure = ex; }
    });
    owner.Start();
    owner.Join();
    if (ownerFailure is not null) throw ownerFailure;
    f.Logger.Error("after-abandonment");
    Require(f.Log.Contains("after-abandonment"), "Abandoned mutex prevented logging.");
    return Task.CompletedTask;
});
await Check("Unwritable log destination signals loss", f =>
{
    string blocked = Path.Combine(f.Root, "not-a-directory");
    File.WriteAllText(blocked, "fixture");
    using var brokenLogger = new FileLogger(blocked);
    bool signalled = false;
    var originalError = Console.Error;
    using var fallback = new StringWriter();
    try
    {
        Console.SetError(fallback);
        try { brokenLogger.Error("lost-marker"); } catch (IOException) { signalled = true; }
    }
    finally { Console.SetError(originalError); }
    signalled |= fallback.ToString().Length > 0;
    Require(signalled, "Log write failed silently: caller has no indication or fallback.");
    return Task.CompletedTask;
});
await Check("Log rotates at 10 MiB", f =>
{
    string folder = Path.Combine(f.Root, DateTime.Now.ToString("yyyy/MM"));
    Directory.CreateDirectory(folder);
    using (var stream = File.Create(Path.Combine(folder, DateTime.Now.ToString("yyyy-MM-dd") + ".log")))
        stream.SetLength(10 * 1024 * 1024);
    f.Logger.Error("rotation-marker");
    Require(Directory.GetFiles(folder, "*_2.log").Any(p => Fixture.ReadShared(p).Contains("rotation-marker")), "No rolled log.");
    return Task.CompletedTask;
});
await Check("Signed download URL is redacted in activation log", async f =>
{
    await f.App().RunAsync(["probe", "--url", "https://example.invalid/model?X-Amz-Signature=SYNTHETIC_SECRET"], default);
    Require(!f.Log.Contains("SYNTHETIC_SECRET"), "Signed URL query is copied verbatim into logs.");
});
await Check("Critical entries persist exception diagnostics", f =>
{
    f.Logger.Critical(new InvalidOperationException("critical-marker", new IOException("critical-inner")).ToString());
    Require(f.Log.Contains("[CRITICAL]") && f.Log.Contains("critical-inner"), "Critical diagnostics missing.");
    return Task.CompletedTask;
});
await Check("URL secrets are removed from metadata, exception and direct WPF-style logs", f =>
{
    const string url = "HTTPS://user:USER_SECRET@example.invalid/model.ifc?custom=QUERY_SECRET&sig=SIGNATURE_SECRET#FRAGMENT_SECRET";
    f.Events.Info("metadata.test", "metadata", new() { ["nested"] = new { url } });
    f.Logger.Error($"event=wpf.test; exception={new IOException(url)}");
    foreach (string secret in new[] { "USER_SECRET", "QUERY_SECRET", "SIGNATURE_SECRET", "FRAGMENT_SECRET" })
        Require(!f.Log.Contains(secret), $"Secret leaked: {secret}");
    Require(f.Log.Contains("example.invalid/model.ifc") && f.Log.Contains("[REDACTED]"), "Useful URL path was lost.");
    // Redaction is confined to the log sink, not the event data consumed by the application.
    using var wire = JsonDocument.Parse(f.Stdout.ToString());
    Require(wire.RootElement.GetProperty("data").GetProperty("nested").GetProperty("url").GetString() == url, "Wire data was mutated.");
    return Task.CompletedTask;
});
await Check("Direct import exception details remain file-only", f =>
{
    Exception failure;
    try { throw new IOException("import-outer", new Exception("import-inner")); }
    catch (Exception ex) { failure = ex; }
    new WorkflowReporter(f.Events).FailedLayerImport("model.3dml", failure);
    Require(f.Log.Contains("import-inner") && f.Log.Contains("Program.cs"), "Import diagnostic details missing.");
    Require(!f.Stderr.ToString().Contains("import-inner"), "Internal exception diagnostics leaked into UI event.");
    return Task.CompletedTask;
});
await Check("Local IFC launcher exception is retained without changing pipe JSON", f =>
{
    var conversion = new IfcConversionResult
    {
        Status = "failed", Message = "launch failed",
        FailureException = new IOException("launch-outer", new Exception("launch-inner"))
    };
    new WorkflowReporter(f.Events).Completed(new FileOperationResult
    {
        Operation = FileOperation.ConvertIfcTo3dml, File = new LocalFileResult { FilePath = "model.ifc" },
        OutputPath = "model.3dml", IfcConversionResult = conversion
    });
    Require(f.Log.Contains("launch-inner"), "IFC launcher cause missing.");
    Require(!JsonSerializer.Serialize(conversion).Contains("launch-inner"), "Local exception serialized on pipe.");
    return Task.CompletedTask;
});
foreach (string status in new[] { "uploaded", "failed", "cancelled" })
{
    await Check($"File log retains import-result {status} payload", f =>
    {
        f.Events.ImportResult(status == "uploaded", status, "outcome", new(), new() { ["success"] = status == "uploaded" });
        Require(f.Log.Contains("type=importResult") && f.Log.Contains($"\"status\":\"{status}\""), "Import payload missing.");
        return Task.CompletedTask;
    });
}
await Check("Failed writes report sanitized JSON and notify UI without throwing", f =>
{
    string blocked = Path.Combine(f.Root, "blocked");
    File.WriteAllText(blocked, "fixture");
    using var logger = new FileLogger(blocked, "WPF");
    string? notification = null;
    logger.WriteFailed += message => notification = message;
    using var output = new StringWriter();
    var previous = Console.Error;
    try
    {
        Console.SetError(output);
        logger.Error("https://example.invalid/file?sig=FALLBACK_SECRET");
    }
    finally { Console.SetError(previous); }
    using var json = JsonDocument.Parse(output.ToString());
    Require(json.RootElement.GetProperty("name").GetString() == "logging.failed", "No structured fallback.");
    Require(notification is not null && logger.LastWriteFailure is not null, "No health/UI signal.");
    Require(!output.ToString().Contains("FALLBACK_SECRET"), "Fallback leaked URL credentials.");
    return Task.CompletedTask;
});
await Check("Logger remains safe when both storage and stderr fail", f =>
{
    string blocked = Path.Combine(f.Root, "blocked");
    File.WriteAllText(blocked, "fixture");
    using var logger = new FileLogger(blocked);
    bool notified = false;
    logger.WriteFailed += _ => { notified = true; throw new Exception("handler failed"); };
    var previous = Console.Error;
    try { Console.SetError(new BrokenWriter()); logger.Error("lost-entry"); }
    finally { Console.SetError(previous); }
    Require(notified && logger.LastWriteFailure is not null, "Failure health signal missing.");
    return Task.CompletedTask;
});
await Check("Mutex timeout reports logging failure instead of silently dropping", f =>
{
    using var ready = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    var owner = new Thread(() =>
    {
        using var mutex = new Mutex(false, @"Local\AccIntegration.SharedLog");
        mutex.WaitOne();
        try { ready.Set(); release.Wait(); }
        finally { mutex.ReleaseMutex(); }
    });
    owner.Start();
    using var output = new StringWriter();
    var previous = Console.Error;
    try
    {
        Require(ready.Wait(TimeSpan.FromSeconds(5)), "Timeout fixture could not own mutex.");
        Console.SetError(output);
        f.Logger.Error("timeout-entry");
    }
    finally { release.Set(); owner.Join(); Console.SetError(previous); }
    Require(output.ToString().Contains("logging.failed") && output.ToString().Contains("TimeoutException"), "Mutex timeout not reported.");
    f.Logger.Error("recovered-entry");
    Require(f.Log.Contains("recovered-entry"), "Logger failed to recover after mutex release.");
    return Task.CompletedTask;
});
Console.WriteLine($"RESULT: {passed} passed, {failed} failed. Evidence: {root}");
return failed == 0 ? 0 : 1;

async Task Check(string name, Func<Fixture, Task> test)
{
    using var fixture = new Fixture(Path.Combine(root, (passed + failed + 1).ToString("D2")));
    try { await test(fixture); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

sealed class Fixture : IDisposable
{
    public string Root { get; }
    public FileLogger Logger { get; }
    public StringWriter Stdout { get; } = new();
    public StringWriter Stderr { get; } = new();
    public EventWriter Events { get; }
    public string Log => string.Join("\n", Directory.GetFiles(Root, "*.log", SearchOption.AllDirectories).Select(ReadShared));
    public static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public Fixture(string root)
    {
        Root = root; Directory.CreateDirectory(root);
        Logger = new FileLogger(root); Events = new EventWriter(Stdout, Stderr, Logger);
    }
    public CliApplication App(ICliCommand? command = null, IAccLicenseService? license = null) =>
        new([command ?? new ProbeCommand(false)], license ?? new LicenseStub(false), Events, Logger, Stderr);
    public void Dispose() { Logger.Dispose(); Stdout.Dispose(); Stderr.Dispose(); }
}
sealed class ProbeCommand(bool requiresLicense, bool fail = false) : ICliCommand
{
    public string Name => "probe";
    public string Usage => "probe";
    public bool RequiresLicense => requiresLicense;
    public Task<int> ExecuteAsync(CliArguments args, CancellationToken token)
    {
        if (fail) throw new InvalidOperationException("outer-marker", new IOException("inner-marker"));
        return Task.FromResult(0);
    }
}
sealed class LicenseStub(bool fail) : IAccLicenseService
{
    public void EnsureLicensed() { if (fail) throw new InvalidOperationException("license-marker"); }
}
sealed class BrokenWriter : StringWriter
{
    public override void WriteLine(string? value) => throw new IOException("stderr unavailable");
}

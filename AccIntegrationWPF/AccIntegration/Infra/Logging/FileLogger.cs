using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AccIntegration.Infra.Logging;

/// <summary>
/// Daily log files under {root}/yyyy/MM/yyyy-MM-dd.log, rolling to _2, _3... past 10 MB.
/// Logging failures are reported without breaking the calling command.
/// </summary>
public sealed class FileLogger : IDisposable
{
    private const long MaxLogFileBytes = 10 * 1024 * 1024;
    private const string LogTimeFormat = "yyyy-MM-dd HH:mm:ss:fff";
    // CLI and WPF instances in this Windows session must use the same name.
    private const string WriteMutexName = @"Local\AccIntegration.SharedLog";
    private static readonly TimeSpan WriteMutexTimeout = TimeSpan.FromSeconds(5);

    private readonly string _rootPath;
    private readonly string? _source;
    private string? _lastWriteFailure;
    private int _reportingFailure;

    public string? LastWriteFailure => Volatile.Read(ref _lastWriteFailure);

    // UI hosts can surface loss even when no stderr is attached. Handlers must not log.
    public event Action<string>? WriteFailed;

    public FileLogger(string rootPath, string? source = null)
    {
        _rootPath = rootPath;
        _source = source;
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERROR", message);

    public void Critical(string message) => Write("CRITICAL", message);

    /// <summary>
    /// Logs crashes that escape every try/catch.
    /// </summary>
    public void LogUnhandledExceptions()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            Critical($"Unhandled exception. IsTerminating={eventArgs.IsTerminating}. {eventArgs.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            Critical($"Unobserved task exception: {eventArgs.Exception}");
    }

    public void Dispose()
    {
        // Writers and mutex handles are scoped to each write; no resources stay open.
    }

    private void Write(string logType, string message)
    {
        string? line = null;
        try
        {
            string timestamp = DateTime.Now.ToString(LogTimeFormat, CultureInfo.InvariantCulture);
            string identifiedMessage = _source is null ? message : $"source={_source}; pid={Environment.ProcessId}; {message}";
            string oneLine = LogRedactor.Redact(identifiedMessage ?? string.Empty)
                .Replace("\r\n", " | ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Replace('\r', ' ');
            line = $"[{logType}] - [{timestamp}] - {oneLine}";

            using var mutex = new Mutex(initiallyOwned: false, name: WriteMutexName);
            bool acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(WriteMutexTimeout);
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner exited without releasing it. We now own it.
                    acquired = true;
                }

                if (!acquired)
                    throw new TimeoutException("Timed out waiting for the shared log mutex.");

                // Select/rotate and open at the current end while holding the mutex.
                // A cached writer can retain a stale position after another process writes.
                string filePath = ResolveWritePath(Encoding.UTF8.GetByteCount(line + Environment.NewLine));
                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.WriteLine(line);
            }
            finally
            {
                // The using declarations above flush and close the file before release.
                if (acquired)
                    mutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            ReportWriteFailure(ex, line);
        }
    }

    private void ReportWriteFailure(Exception exception, string? sanitizedEntry)
    {
        const string message = "The application log could not be written. Check log-folder permissions, disk space, and other running instances.";
        Volatile.Write(ref _lastWriteFailure, message);
        if (Interlocked.Exchange(ref _reportingFailure, 1) != 0)
            return;
        try
        {
            try
            {
                // Independent sink: never call this logger recursively to report its own failure.
                Console.Error.WriteLine(JsonSerializer.Serialize(new
                {
                    name = "logging.failed", level = "error", message, timestampUtc = DateTime.UtcNow,
                    data = new
                    {
                        source = _source, pid = Environment.ProcessId,
                        exceptionType = exception.GetType().FullName,
                        reason = LogRedactor.Redact(exception.Message), failedEntry = sanitizedEntry
                    }
                }));
                Console.Error.Flush();
            }
            catch { /* stderr may also be unavailable; the health state and UI signal remain. */ }
            try { WriteFailed?.Invoke(message); }
            catch { /* A failing notification handler must not break the application. */ }
        }
        finally { Volatile.Write(ref _reportingFailure, 0); }
    }

    private string ResolveWritePath(int requiredBytes)
    {
        DateTime now = DateTime.Now;
        string dateKey = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string monthDirectory = Path.Combine(
            _rootPath,
            now.ToString("yyyy", CultureInfo.InvariantCulture),
            now.ToString("MM", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(monthDirectory);

        return ResolveFilePath(monthDirectory, dateKey, requiredBytes);
    }

    private static string ResolveFilePath(string monthDirectory, string dateKey, int requiredBytes)
    {
        for (int index = 1; ; index++)
        {
            string fileName = index <= 1 ? $"{dateKey}.log" : $"{dateKey}_{index}.log";
            string candidate = Path.Combine(monthDirectory, fileName);

            if (!File.Exists(candidate) || new FileInfo(candidate).Length + requiredBytes <= MaxLogFileBytes)
                return candidate;
        }
    }
}

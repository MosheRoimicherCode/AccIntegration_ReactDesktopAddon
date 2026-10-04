using System.Globalization;
using System.Text;

namespace AccIntegrationCli.Output;

/// <summary>
/// Daily log files under {root}/yyyy/MM/yyyy-MM-dd.log, rolling to _2, _3... past 10 MB.
/// Logging failures are swallowed so they never break a command.
/// </summary>
public sealed class FileLogger : IDisposable
{
    private const long MaxLogFileBytes = 10 * 1024 * 1024;
    private const string LogTimeFormat = "yyyy-MM-dd HH:mm:ss:fff";

    private readonly object _sync = new();
    private readonly string _rootPath;
    private StreamWriter? _writer;
    private string _filePath = string.Empty;
    private string _dateKey = string.Empty;

    public FileLogger(string rootPath)
    {
        _rootPath = rootPath;
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
        lock (_sync)
        {
            try
            {
                _writer?.Dispose();
            }
            catch
            {
                // Ignore shutdown logging errors.
            }

            _writer = null;
        }
    }

    private void Write(string logType, string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString(LogTimeFormat, CultureInfo.InvariantCulture);
            string oneLine = (message ?? string.Empty)
                .Replace("\r\n", " | ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Replace('\r', ' ');
            string line = $"[{logType}] - [{timestamp}] - {oneLine}";

            lock (_sync)
            {
                EnsureWriter(Encoding.UTF8.GetByteCount(line + Environment.NewLine));
                _writer?.WriteLine(line);
            }
        }
        catch
        {
            // Ignore logging failures to avoid breaking command flow.
        }
    }

    private void EnsureWriter(int requiredBytes)
    {
        DateTime now = DateTime.Now;
        string dateKey = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (_writer is not null &&
            string.Equals(_dateKey, dateKey, StringComparison.Ordinal) &&
            new FileInfo(_filePath).Length + requiredBytes <= MaxLogFileBytes)
        {
            return;
        }

        _writer?.Dispose();
        _writer = null;

        string monthDirectory = Path.Combine(
            _rootPath,
            now.ToString("yyyy", CultureInfo.InvariantCulture),
            now.ToString("MM", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(monthDirectory);

        _dateKey = dateKey;
        _filePath = ResolveFilePath(monthDirectory, dateKey, requiredBytes);

        var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
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

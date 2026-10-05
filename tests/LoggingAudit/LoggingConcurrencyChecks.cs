using System.Diagnostics;
using System.Reflection;
using AccIntegration.Infra.Logging;

internal static class LoggingConcurrencyChecks
{
    private const int WorkerCount = 3;
    private const int EntriesPerWorker = 100;
    private const long MaxBytes = 10 * 1024 * 1024;

    public static int RunWorker(string root, string id)
    {
        using var logger = new FileLogger(root);
        logger.Error(Marker(id, 0));
        Console.WriteLine("ready");
        if (Console.ReadLine() != "go") return 2;
        for (int i = 1; i < EntriesPerWorker; i++)
            logger.Error(Marker(id, i));
        return 0;
    }

    public static async Task VerifyAsync(string root, bool rotate)
    {
        if (rotate)
        {
            var now = DateTime.Now;
            string folder = Path.Combine(root, now.ToString("yyyy/MM"));
            Directory.CreateDirectory(folder);
            using var stream = File.Create(Path.Combine(folder, now.ToString("yyyy-MM-dd") + ".log"));
            // Leave room for initial writes; concurrent writes must cross the boundary.
            stream.SetLength(MaxBytes - 1024);
        }

        var workers = new List<Process>();
        try
        {
            for (int i = 0; i < WorkerCount; i++)
            {
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                start.ArgumentList.Add("--log-worker");
                start.ArgumentList.Add(root);
                start.ArgumentList.Add(i.ToString());
                var worker = Process.Start(start) ?? throw new Exception("Could not start log worker.");
                workers.Add(worker);
                if (await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) != "ready")
                    throw new Exception("Log worker did not become ready.");
            }
            foreach (var worker in workers)
            {
                await worker.StandardInput.WriteLineAsync("go");
                await worker.StandardInput.FlushAsync();
            }
            await Task.WhenAll(workers.Select(p => p.WaitForExitAsync())).WaitAsync(TimeSpan.FromSeconds(20));
            foreach (var worker in workers)
                if (worker.ExitCode != 0) throw new Exception($"Log worker failed: {await worker.StandardError.ReadToEndAsync()}");

            string[] files = Directory.GetFiles(root, "*.log", SearchOption.AllDirectories);
            string[] entries = files.SelectMany(File.ReadAllLines).Where(line => line.Contains("worker-")).ToArray();
            if (entries.Length != WorkerCount * EntriesPerWorker)
                throw new Exception($"Expected {WorkerCount * EntriesPerWorker} complete entries; got {entries.Length}.");
            for (int worker = 0; worker < WorkerCount; worker++)
                for (int i = 0; i < EntriesPerWorker; i++)
                    if (entries.Count(line => line.EndsWith(" - " + Marker(worker.ToString(), i), StringComparison.Ordinal)) != 1)
                        throw new Exception($"Missing, duplicated, or corrupt entry: {Marker(worker.ToString(), i)}");
            if (rotate && (files.Length < 2 || files.Any(file => new FileInfo(file).Length > MaxBytes)))
                throw new Exception("Rotation failed or exceeded the per-file size limit.");
        }
        finally
        {
            foreach (var worker in workers)
            {
                if (!worker.HasExited) { worker.Kill(entireProcessTree: true); await worker.WaitForExitAsync(); }
                worker.Dispose();
            }
        }
    }

    private static string Marker(string id, int entry) => $"worker-{id}-entry-{entry:D3}";
}

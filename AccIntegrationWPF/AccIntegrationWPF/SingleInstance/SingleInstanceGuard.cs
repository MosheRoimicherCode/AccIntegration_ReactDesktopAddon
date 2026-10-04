namespace AccIntegrationWPF.SingleInstance;

/// <summary>
/// Owns a per-user named mutex. Only the first process becomes primary; later launches forward their arguments.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\AccIntegrationWPF.SingleInstance";

    private readonly Mutex _mutex;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public void Dispose()
    {
        if (IsPrimary)
            _mutex.ReleaseMutex();

        _mutex.Dispose();
    }
}

using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.Conversion;

public enum ConversionStage
{
    // Reading the IFC placement.
    Preparing,

    // Placement shown to the user; waiting for Convert.
    AwaitingInput,

    Converting
}

/// <summary>
/// Runs handed-over IFCs one at a time, in arrival order: read placement → wait for the user's
/// Convert (with their edits) → convert, save and import. Use from the UI thread: events are raised on it.
/// </summary>
public sealed class ConversionQueue
{
    private readonly IIfcConversionOrchestrator _orchestrator;
    private readonly Queue<LaunchArguments> _pending = new();
    private TaskCompletionSource<IfcPlacement>? _userInput;
    private bool _started;
    private bool _running;

    public ConversionQueue(IIfcConversionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public event Action<LaunchArguments>? JobStarted;

    // The IFC placement is ready for the user to review.
    public event Action<LaunchArguments, IfcPlacement>? JobReady;

    public event Action<double>? JobProgress;

    // Exception is null on success.
    public event Action<LaunchArguments, Exception?>? JobCompleted;

    // A snapshot should be sent whenever membership or the current stage changes.
    public event Action? QueueChanged;

    public IReadOnlyList<LaunchArguments> Pending => _pending.ToArray();

    public LaunchArguments? Current { get; private set; }

    public ConversionStage Stage { get; private set; }

    public IfcPlacement? CurrentPlacement { get; private set; }

    public double LastProgress { get; private set; }

    public void Enqueue(LaunchArguments request)
    {
        if (!request.HasConversionRequest)
            return;

        _pending.Enqueue(request);
        QueueChanged?.Invoke();
        _ = ProcessAsync();
    }

    /// <summary>
    /// Begins processing; called once the UI can show the job.
    /// </summary>
    public void Start()
    {
        _started = true;
        _ = ProcessAsync();
    }

    /// <summary>
    /// The user clicked Convert with this placement (edited or as read from the IFC).
    /// </summary>
    public void Submit(IfcPlacement placement)
    {
        if (Stage != ConversionStage.AwaitingInput || _userInput is null)
            throw new InvalidOperationException("No IFC is waiting to be converted.");

        _userInput.TrySetResult(placement);
    }

    private async Task ProcessAsync()
    {
        if (!_started || _running)
            return;

        _running = true;
        try
        {
            while (_pending.TryDequeue(out LaunchArguments? request))
            {
                Exception? failure = null;
                try
                {
                    await RunJobAsync(request);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                Current = null;
                CurrentPlacement = null;
                _userInput = null;
                JobCompleted?.Invoke(request, failure);
                QueueChanged?.Invoke();
            }
        }
        finally
        {
            _running = false;
        }
    }

    private async Task RunJobAsync(LaunchArguments request)
    {
        Current = request;
        Stage = ConversionStage.Preparing;
        LastProgress = 0;
        JobStarted?.Invoke(request);
        QueueChanged?.Invoke();

        IfcPlacement placement = await _orchestrator.PrepareAsync(request.IfcPath!);

        _userInput = new TaskCompletionSource<IfcPlacement>();
        CurrentPlacement = placement;
        Stage = ConversionStage.AwaitingInput;
        JobReady?.Invoke(request, placement);
        QueueChanged?.Invoke();

        IfcPlacement confirmed = await _userInput.Task;

        Stage = ConversionStage.Converting;
        QueueChanged?.Invoke();

        // Progress<T> captures the UI context, so reports arrive on the UI thread.
        var progress = new Progress<double>(value =>
        {
            LastProgress = value;
            JobProgress?.Invoke(value);
        });

        await _orchestrator.RunAsync(request.IfcPath!, request.OutputPath!, confirmed, progress);
    }
}

using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegration.Infra.Logging;

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
    private readonly FileLogger _logger;
    private readonly Queue<LaunchArguments> _pending = new();
    private readonly Dictionary<LaunchArguments, IfcPlacement> _deferredPlacements = new();
    private TaskCompletionSource<IfcPlacement>? _userInput;
    private CancellationTokenSource? _currentCancellation;
    private bool _started;
    private bool _running;

    public ConversionQueue(IIfcConversionOrchestrator orchestrator, FileLogger logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public bool IsIdle => Current is null && _pending.Count == 0;

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

    public void MovePending(int fromIndex, int toIndex)
    {
        LaunchArguments[] items = _pending.ToArray();
        if (fromIndex < 0 || fromIndex >= items.Length || toIndex < 0 || toIndex >= items.Length || fromIndex == toIndex)
            return;

        (items[fromIndex], items[toIndex]) = (items[toIndex], items[fromIndex]);
        _pending.Clear();
        foreach (LaunchArguments item in items)
            _pending.Enqueue(item);

        QueueChanged?.Invoke();
    }

    public bool CancelCurrent()
    {
        if (Current is null || _currentCancellation is null)
            return false;

        _currentCancellation.Cancel();
        _userInput?.TrySetCanceled(_currentCancellation.Token);
        return true;
    }

    public bool SwitchToNextPending(IfcPlacement editedPlacement)
    {
        if (Current is not { } current || Stage != ConversionStage.AwaitingInput ||
            _userInput is null || _userInput.Task.IsCompleted || _currentCancellation?.IsCancellationRequested != false ||
            CurrentPlacement is not { } placement || _pending.Count == 0)
            return false;

        LaunchArguments next = _pending.Dequeue();
        LaunchArguments[] remaining = _pending.ToArray();
        _pending.Clear();
        _pending.Enqueue(next);
        _pending.Enqueue(current);
        foreach (LaunchArguments request in remaining)
            _pending.Enqueue(request);

        // Reuse the coordinates already read when this model returns to the front.
        _deferredPlacements[current] = new IfcPlacement
        {
            X = editedPlacement.X,
            Y = editedPlacement.Y,
            Z = editedPlacement.Z,
            HasPivot = placement.HasPivot,
            CoordinateSystem = editedPlacement.CoordinateSystem
        };
        _userInput.TrySetException(new DeferredJobException());
        return true;
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
                bool deferred = false;
                using var cancellation = new CancellationTokenSource();
                _currentCancellation = cancellation;
                try
                {
                    await RunJobAsync(request, cancellation.Token);
                }
                catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested)
                {
                    failure = new OperationCanceledException("Cancelled by user.", ex, cancellation.Token);
                }
                catch (DeferredJobException)
                {
                    deferred = true;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                Current = null;
                CurrentPlacement = null;
                _userInput = null;
                _currentCancellation = null;
                if (!deferred)
                {
                    string context = $"ifcPath={request.IfcPath}; outputPath={request.OutputPath}";
                    if (failure is null)
                        _logger.Info($"event=wpf.import.completed; status=uploaded; {context}");
                    else if (failure is OperationCanceledException)
                        _logger.Info($"event=wpf.import.cancelled; status=cancelled; {context}");
                    else
                        _logger.Error($"event=wpf.import.failed; status=failed; {context}; exception={failure}");
                    JobCompleted?.Invoke(request, failure);
                }
                QueueChanged?.Invoke();
            }
        }
        finally
        {
            _running = false;
        }
    }

    private async Task RunJobAsync(LaunchArguments request, CancellationToken cancellationToken)
    {
        Current = request;
        Stage = ConversionStage.Preparing;
        LastProgress = 0;
        JobStarted?.Invoke(request);
        QueueChanged?.Invoke();

        IfcPlacement placement;
        if (!_deferredPlacements.Remove(request, out placement!))
            placement = await _orchestrator.PrepareAsync(request.IfcPath!, cancellationToken);

        _userInput = new TaskCompletionSource<IfcPlacement>(TaskCreationOptions.RunContinuationsAsynchronously);
        CurrentPlacement = placement;
        Stage = ConversionStage.AwaitingInput;
        JobReady?.Invoke(request, placement);
        QueueChanged?.Invoke();

        IfcPlacement confirmed = await _userInput.Task.WaitAsync(cancellationToken);

        Stage = ConversionStage.Converting;
        QueueChanged?.Invoke();

        // Progress<T> captures the UI context, so reports arrive on the UI thread.
        var progress = new Progress<double>(value =>
        {
            LastProgress = value;
            JobProgress?.Invoke(value);
        });

        await _orchestrator.RunAsync(request.IfcPath!, request.OutputPath!, confirmed, progress, cancellationToken);
    }

    private sealed class DeferredJobException : Exception { }
}

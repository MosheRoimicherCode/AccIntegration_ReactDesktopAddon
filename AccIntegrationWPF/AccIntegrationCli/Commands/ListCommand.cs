using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;
using AccIntegrationCli.Input;
using AccIntegrationCli.Output;

namespace AccIntegrationCli.Commands;

/// <summary>
/// Returns every metadata record, newest first.
/// </summary>
public sealed class ListCommand : ICliCommand
{
    private readonly IQueryLocalMetadata _metadataQuery;
    private readonly EventWriter _events;

    public ListCommand(IQueryLocalMetadata metadataQuery, EventWriter events)
    {
        _metadataQuery = metadataQuery ?? throw new ArgumentNullException(nameof(metadataQuery));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public string Name => "list";

    public string Usage => "list";

    public bool RequiresLicense => true;

    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken)
    {
        args.EnsureNoOptions();

        IReadOnlyList<DownloadedFileMetadata> records = await _metadataQuery
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        _events.Info(
            "metadata.list.completed",
            $"Total records: {records.Count}.",
            new Dictionary<string, object?>
            {
                ["count"] = records.Count,
                ["items"] = records
            });
        return 0;
    }
}

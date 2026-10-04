using System.Text.Json;
using AccIntegration.Core.Interfaces;
using AccIntegration.Core.Models;

namespace AccIntegration.Infra.Services;

/// <summary>
/// Sharded JSON file storage for metadata records (1.json, 2.json, ...).
/// Shared by the metadata writer and reader so both go through the same lock.
/// </summary>
internal sealed class MetadataShardStore
{
    private const int MaxRecordsPerShard = 500;

    // Static so separate writer/reader instances in the process never read a half-written shard.
    private static readonly SemaphoreSlim Mutex = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _databaseDirectoryPath;

    public MetadataShardStore(IDownloadStoragePathProvider pathProvider)
    {
        if (pathProvider is null)
            throw new ArgumentNullException(nameof(pathProvider));

        _databaseDirectoryPath = pathProvider.MetadataDatabaseFolderPath;
    }

    public async Task AppendAsync(DownloadedFileMetadata metadata, CancellationToken cancellationToken)
    {
        await Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Shard> shards = await ReadShardsAsync(cancellationToken).ConfigureAwait(false);

            Shard target = shards.Count > 0 && shards[^1].Records.Count < MaxRecordsPerShard
                ? shards[^1]
                : new Shard(NextShardIndex(shards), new List<DownloadedFileMetadata>());

            target.Records.Add(metadata);
            await WriteShardAsync(target, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Mutex.Release();
        }
    }

    /// <summary>
    /// Replaces the record with the same Id. Returns false when no record matches.
    /// </summary>
    public async Task<bool> UpdateAsync(DownloadedFileMetadata metadata, CancellationToken cancellationToken)
    {
        await Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Shard> shards = await ReadShardsAsync(cancellationToken).ConfigureAwait(false);
            foreach (Shard shard in shards)
            {
                int index = shard.Records.FindIndex(record => record.Id == metadata.Id);
                if (index < 0)
                    continue;

                shard.Records[index] = metadata;
                await WriteShardAsync(shard, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        finally
        {
            Mutex.Release();
        }
    }

    /// <summary>
    /// Removes the record with the given Id. Returns false when no record matches.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Shard> shards = await ReadShardsAsync(cancellationToken).ConfigureAwait(false);
            foreach (Shard shard in shards)
            {
                int index = shard.Records.FindIndex(record => record.Id == id);
                if (index < 0)
                    continue;

                shard.Records.RemoveAt(index);
                await WriteShardAsync(shard, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        finally
        {
            Mutex.Release();
        }
    }

    /// <summary>
    /// Returns all records in insertion order (oldest first).
    /// </summary>
    public async Task<List<DownloadedFileMetadata>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await Mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Shard> shards = await ReadShardsAsync(cancellationToken).ConfigureAwait(false);
            return shards.SelectMany(shard => shard.Records).ToList();
        }
        finally
        {
            Mutex.Release();
        }
    }

    private async Task<List<Shard>> ReadShardsAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_databaseDirectoryPath);

        var shards = new List<Shard>();
        foreach (string shardPath in Directory.GetFiles(_databaseDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            int shardIndex = ParseShardIndex(shardPath);
            if (shardIndex <= 0)
                continue;

            List<DownloadedFileMetadata> records = await ReadRecordsAsync(shardPath, cancellationToken).ConfigureAwait(false);
            shards.Add(new Shard(shardIndex, records));
        }

        return shards.OrderBy(shard => shard.Index).ToList();
    }

    private static async Task<List<DownloadedFileMetadata>> ReadRecordsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length == 0)
            return new List<DownloadedFileMetadata>();

        List<DownloadedFileMetadata> records = await JsonSerializer
            .DeserializeAsync<List<DownloadedFileMetadata>>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? new List<DownloadedFileMetadata>();

        // The deserializer drops the dictionary comparer; restore case-insensitive keys.
        foreach (DownloadedFileMetadata record in records)
        {
            record.Properties = new Dictionary<string, string>(
                record.Properties ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        return records;
    }

    private async Task WriteShardAsync(Shard shard, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_databaseDirectoryPath, $"{shard.Index}.json");

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, shard.Records, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static int NextShardIndex(List<Shard> shards)
    {
        return shards.Count == 0 ? 1 : shards[^1].Index + 1;
    }

    private static int ParseShardIndex(string shardPath)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(shardPath);
        return int.TryParse(fileNameWithoutExtension, out int value) && value > 0 ? value : -1;
    }

    private sealed record Shard(int Index, List<DownloadedFileMetadata> Records);
}

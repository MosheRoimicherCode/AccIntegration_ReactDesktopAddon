using System.Net.Http.Headers;
using AccIntegration.Core.Interfaces;

namespace AccIntegration.Infra.Services;

public sealed class DownloadedFileService : IDownloadedFileService
{
    private static readonly HttpClient SharedHttpClient = new();

    private readonly HttpClient _httpClient;

    public DownloadedFileService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<string?> DownloadFileAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));

        using HttpResponseMessage response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var targetStream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);

        return ResolveServerFileName(url, response.Content.Headers.ContentDisposition);
    }

    private static string? ResolveServerFileName(string url, ContentDispositionHeaderValue? contentDisposition)
    {
        string? fileName = contentDisposition?.FileNameStar;
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = contentDisposition?.FileName;

        if (string.IsNullOrWhiteSpace(fileName) && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            fileName = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty);

        fileName = fileName?.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }
}

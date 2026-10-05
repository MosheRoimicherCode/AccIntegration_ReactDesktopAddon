using System.Text.RegularExpressions;

namespace AccIntegration.Infra.Logging;

/// <summary>Removes URL credentials at the log sink, including URLs inside JSON and exceptions.</summary>
internal static class LogRedactor
{
    private static readonly Regex HttpUrl = new(
        @"https?://[^\s""<>]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static string Redact(string text)
    {
        try
        {
            return HttpUrl.Replace(text, match =>
            {
                string url = match.Value;
                // Drop the entire query/fragment: providers use different credential names.
                int suffix = url.IndexOfAny(['?', '#']);
                string address = suffix >= 0 ? url[..suffix] : url;
                int authority = address.IndexOf("://", StringComparison.Ordinal) + 3;
                int path = address.IndexOf('/', authority);
                int at = address.LastIndexOf('@', (path >= 0 ? path : address.Length) - 1);
                if (at >= authority)
                    address = address[..authority] + "[REDACTED]@" + address[(at + 1)..];
                return suffix >= 0 ? address + "?[REDACTED]" : address;
            });
        }
        catch (RegexMatchTimeoutException)
        {
            // Never fall back to writing the unsanitized message.
            return "[Log message omitted: URL redaction timed out.]";
        }
    }
}

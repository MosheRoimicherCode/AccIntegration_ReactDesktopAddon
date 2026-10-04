using System.Reflection;

namespace AccIntegrationWPF.Bridge;

/// <summary>
/// Turns an exception into one readable line for the user, including inner causes
/// (e.g. a TerraExplorer COM error wrapped by our own message).
/// </summary>
public static class ErrorText
{
    public static string From(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            // Wrapper exceptions only repeat their inner message.
            if (current is TargetInvocationException or AggregateException && current.InnerException is not null)
                continue;

            string message = current.Message.Trim();
            if (message.Length > 0 && !messages.Contains(message))
                messages.Add(message);
        }

        return messages.Count > 0 ? string.Join(" → ", messages) : exception.GetType().Name;
    }
}

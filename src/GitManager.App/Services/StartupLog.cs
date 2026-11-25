using System.Text;

namespace GitManager.App.Services;

/// <summary>
/// Process-level startup log under LocalAppData, independent of the user workspace.
/// Logging failures must never crash the application.
/// </summary>
public static class StartupLog
{
    public const string FileName = "startup.log";

    public static string FilePath => Path.Combine(AppLocalState.LogsDirectory, FileName);

    public static void Write(string eventName, string? detail = null, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(AppLocalState.LogsDirectory);
            var text = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("O"))
                .Append(' ')
                .Append(eventName);
            if (!string.IsNullOrWhiteSpace(detail))
                text.Append(' ').Append(Redact(detail));
            if (exception is not null)
            {
                text.AppendLine();
                text.Append(exception);
            }

            File.AppendAllText(FilePath, text.AppendLine().ToString());
        }
        catch
        {
            // Logging must never fail the calling operation.
        }
    }

    public static string SanitizeArguments(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(RedactArgument));

    internal static string RedactArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return argument;
        var lower = argument.ToLowerInvariant();
        if (lower.Contains("token") || lower.Contains("password") || lower.Contains("secret") ||
            lower.Contains("pat=") || lower.Contains("://") && argument.Contains('@'))
            return "[redacted]";
        return argument;
    }

    private static string Redact(string value) => RedactArgument(value);
}

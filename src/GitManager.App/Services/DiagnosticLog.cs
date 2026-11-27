using System.Diagnostics;
using System.Text;

namespace GitManager.App.Services;

/// <summary>
/// Append-only technical log. User-facing status must not copy exception text or paths from here.
/// </summary>
public static class DiagnosticLog
{
    public const string FileName = "git-manager.log";

    public static string DirectoryFor(string workspaceRoot) =>
        Path.Combine(workspaceRoot, "00_Manager", "logs");

    public static string FilePath(string logDirectory) => Path.Combine(logDirectory, FileName);

    public static void Write(string logDirectory, string category, string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            var text = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("O"))
                .Append(" [")
                .Append(category)
                .Append("] ")
                .Append(message);
            if (exception is not null)
            {
                text.AppendLine();
                text.Append(exception);
            }

            File.AppendAllText(FilePath(logDirectory), text.AppendLine().ToString());
        }
        catch
        {
            // Logging must never fail the calling operation.
        }

        try
        {
            Trace.WriteLine($"GitManager [{category}] {message}");
            if (exception is not null) Trace.WriteLine(exception.ToString());
        }
        catch { }
    }
}

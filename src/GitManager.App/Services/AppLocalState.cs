namespace GitManager.App.Services;

/// <summary>
/// Machine-local Git Manager state. This is never stored under the install directory
/// and must not be deleted by the installer or uninstaller.
/// </summary>
public static class AppLocalState
{
    public const string FolderName = "GitManager";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName);

    public static string SettingsPath => Path.Combine(Root, "settings.json");
    public static string JournalPath => Path.Combine(Root, "operations.sqlite3");
    public static string LockDirectory => Path.Combine(Root, "operation-locks");
}

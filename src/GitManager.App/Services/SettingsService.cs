using System.Text.Json;

namespace GitManager.App.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GitManager", "settings.json");

    public async Task<string?> LoadWorkspaceAsync()
    {
        if (!File.Exists(_settingsPath)) return null;
        try
        {
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                File.OpenRead(_settingsPath));
            return settings?.WorkspacePath;
        }
        catch (JsonException) { return null; }
    }

    public async Task SaveWorkspaceAsync(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, new AppSettings(path));
    }

    private sealed record AppSettings(string WorkspacePath);
}

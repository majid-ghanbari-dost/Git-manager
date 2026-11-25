using System.Text.Json;

namespace GitManager.App;

internal sealed class ThemePreferenceService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GitManager", "theme.json");

    public async Task<string> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path)) return "System";
            var json = await File.ReadAllTextAsync(_path);
            var mode = JsonSerializer.Deserialize<ThemeSettings>(json)?.Mode;
            return mode is "Light" or "Dark" or "System" ? mode : "System";
        }
        catch
        {
            return "System";
        }
    }

    public async Task SaveAsync(string mode)
    {
        if (mode is not ("Light" or "Dark" or "System"))
            throw new ArgumentOutOfRangeException(nameof(mode));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(new ThemeSettings(mode)));
    }

    private sealed record ThemeSettings(string Mode);
}

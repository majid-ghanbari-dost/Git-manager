namespace GitManager.App.Services;

public sealed class WorkspaceService
{
    public void Initialize(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        foreach (var folder in new[] { "00_Manager/catalog", "00_Manager/logs", "00_Manager/temp",
                     "01_Active", "02_On-Hold", "03_Archived", "04_Quarantine", "05_Exports" })
            Directory.CreateDirectory(Path.Combine(fullRoot, folder));
    }

    public string GetProjectContainer(string root, string lifecycle, string category, string owner, string repository)
    {
        var lifecycleFolder = lifecycle switch
        {
            "Active" => "01_Active",
            "On-Hold" => "02_On-Hold",
            "Archived" => "03_Archived",
            _ => "04_Quarantine"
        };
        var path = Path.GetFullPath(Path.Combine(root, lifecycleFolder,
            PathSafety.SafeSegment(category), PathSafety.SafeSegment(owner), PathSafety.SafeSegment(repository)));
        if (!PathSafety.IsWithin(root, path))
            throw new InvalidOperationException("مسیر مقصد خارج از کتابخانه است.");
        return path;
    }
}

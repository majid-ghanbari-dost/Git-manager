namespace GitManager.App.Models;

public sealed class ProjectRecord
{
    public string ProjectId { get; set; } = Guid.NewGuid().ToString("N");
    public string CanonicalRemoteUrl { get; set; } = "";
    public string Host { get; set; } = "github.com";
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string Category { get; set; } = "Unclassified";
    public string Lifecycle { get; set; } = "Active";
    public string Availability { get; set; } = "Unknown";
    public string ContainerPath { get; set; } = "";
    public string RepositoryPath { get; set; } = "";
    public string CurrentBranch { get; set; } = "";
    public string CurrentCommit { get; set; } = "";
    public string? LastOperationStatus { get; set; }
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastBackupAtUtc { get; set; }
    public DateTime? LifecycleChangedAtUtc { get; set; }

    public string AvailabilityDisplay => Availability switch
    {
        "Available" => "موجود",
        "Missing" => "مفقود",
        "PathDrift" => "ناسازگار",
        _ => "نامشخص"
    };
}

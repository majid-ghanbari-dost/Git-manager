namespace GitManager.App.Models;

public static class LifecycleState
{
    public const string Active = "Active";
    public const string OnHold = "On-Hold";
    public const string Archived = "Archived";

    public const string Available = "Available";
    public const string Missing = "Missing";
    public const string PathDrift = "PathDrift";
    public const string Unknown = "Unknown";

    public const string CatalogCommitted = "CatalogCommitted";
    public const string Completed = "Completed";
    public const string RolledBack = "RolledBack";
    public const string RecoveryRequired = "RecoveryRequired";

    public static bool IsKnown(string? lifecycle) =>
        lifecycle is Active or OnHold or Archived;

    public static string Parse(string? lifecycle)
    {
        if (IsKnown(lifecycle)) return lifecycle!;
        throw new InvalidOperationException(
            $"وضعیت چرخه عمر «{lifecycle}» نامعتبر است و رد شد.");
    }

    public static bool IsNoOp(string from, string to) =>
        string.Equals(from, to, StringComparison.Ordinal);

    public static bool RequiresArchiveConfirm(string from, string to) =>
        to == Archived && from != Archived;

    public static bool RequiresRestoreConfirm(string from, string to) =>
        from == Archived && to != Archived;

    public static bool IsDenied(string from, string to) =>
        !IsKnown(from) || !IsKnown(to);
}

public sealed record LifecycleChangeOptions(
    bool ConfirmDirtyWorkingTree = false,
    bool ConfirmArchive = false,
    bool ConfirmRestoreFromArchive = false);

public sealed record LifecycleEventRecord(
    long EventSeq,
    string EventId,
    string ProjectId,
    string FromLifecycle,
    string ToLifecycle,
    string? OperationId,
    string Result,
    string? GitStatusSummary,
    string? AvailabilityAtStart,
    string? Message,
    DateTime CreatedAtUtc);

namespace GitManager.App.Models;

public sealed record CategoryRecord(
    string CategoryId,
    string Name,
    bool IsSystem,
    int SortOrder,
    DateTime CreatedAtUtc);

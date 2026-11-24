using GitManager.App.Services;

namespace GitManager.App.ViewModels;

public static class CloneCommandGate
{
    public const string LibraryClosedReason = "کتابخانه باز نیست. ابتدا محل کتابخانه را انتخاب کنید.";
    public const string MissingUrlReason = "نشانی مخزن GitHub را وارد کنید.";
    public const string JournalUnavailableReason = JournalUnavailableException.UserMessage;

    public static bool CanExecute(bool libraryOpen, bool journalUsable, string? repositoryUrl) =>
        libraryOpen && journalUsable && !string.IsNullOrWhiteSpace(repositoryUrl);

    public static string DisabledReason(bool libraryOpen, bool journalUsable, string? repositoryUrl)
    {
        if (!libraryOpen) return LibraryClosedReason;
        if (!journalUsable) return JournalUnavailableReason;
        if (string.IsNullOrWhiteSpace(repositoryUrl)) return MissingUrlReason;
        return "";
    }
}

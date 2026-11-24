namespace GitManager.App.ViewModels;

public static class CloneCommandGate
{
    public const string LibraryClosedReason = "کتابخانه باز نیست. ابتدا محل کتابخانه را انتخاب کنید.";
    public const string MissingUrlReason = "نشانی مخزن GitHub را وارد کنید.";

    public static bool CanExecute(bool libraryOpen, string? repositoryUrl) =>
        libraryOpen && !string.IsNullOrWhiteSpace(repositoryUrl);

    public static string DisabledReason(bool libraryOpen, string? repositoryUrl)
    {
        if (!libraryOpen) return LibraryClosedReason;
        if (string.IsNullOrWhiteSpace(repositoryUrl)) return MissingUrlReason;
        return "";
    }
}

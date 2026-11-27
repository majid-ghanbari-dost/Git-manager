namespace GitManager.App.Services;

public sealed class JournalUnavailableException : InvalidOperationException
{
    public const string UserMessage =
        "دفتر ثبت عملیات قابل استفاده نیست. برای جلوگیری از پروژهٔ نیمه‌کاره، دریافت مخزن انجام نمی‌شود.";

    public JournalUnavailableException(Exception? innerException = null)
        : base(UserMessage, innerException)
    {
    }
}

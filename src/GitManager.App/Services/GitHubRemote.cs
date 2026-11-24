namespace GitManager.App.Services;

public sealed record GitHubRemote(string Owner, string Repository, string CanonicalUrl)
{
    public static GitHubRemote Parse(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("فقط نشانی معتبر مخزن در github.com پشتیبانی می‌شود.");

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException("نشانی باید به شکل https://github.com/owner/repository باشد.");

        var owner = parts[0];
        var repository = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4] : parts[1];

        if (!IsSafeSegment(owner) || !IsSafeSegment(repository))
            throw new InvalidOperationException("نام مالک یا مخزن معتبر نیست.");

        return new GitHubRemote(owner, repository, $"https://github.com/{owner}/{repository}.git");
    }

    private static bool IsSafeSegment(string value) =>
        value.Length is > 0 and <= 100 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

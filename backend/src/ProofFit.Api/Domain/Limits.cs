namespace ProofFit.Api.Domain;

/// <summary>Input limits shared by request validation and the database schema.</summary>
public static class Limits
{
    public const int FileNameLength = 255;
    public const long ResumeFileBytes = 5 * 1024 * 1024;

    // A resume is one to three pages; ten leaves room for a long academic CV while keeping a
    // crafted thousand-page file from tying up the server.
    public const int ResumePages = 10;

    // About ten dense pages. Longer text would not fit a model's prompt alongside the vacancy.
    public const int ResumeTextLength = 40_000;

    // Reading an ordinary resume takes well under a second; a file that takes this long is
    // either malformed or built to be slow.
    public static readonly TimeSpan ResumeReadTime = TimeSpan.FromSeconds(10);

    public const int GitHubLoginLength = 39;

    public const int VacancyTitleLength = 200;
    public const int VacancyTextMinLength = 50;
    public const int VacancyTextMaxLength = 20_000;
}

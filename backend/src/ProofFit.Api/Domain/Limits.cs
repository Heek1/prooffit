namespace ProofFit.Api.Domain;

/// <summary>Input limits shared by request validation and the database schema.</summary>
public static class Limits
{
    public const int FileNameLength = 255;
    public const long ResumeFileBytes = 5 * 1024 * 1024;

    public const int GitHubLoginLength = 39;

    public const int VacancyTitleLength = 200;
    public const int VacancyTextMinLength = 50;
    public const int VacancyTextMaxLength = 20_000;
}

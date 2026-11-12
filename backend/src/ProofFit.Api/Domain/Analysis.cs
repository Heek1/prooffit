namespace ProofFit.Api.Domain;

public enum AnalysisStatus { Queued, Running, Completed, Failed }

/// <summary>The step a running analysis is on, shown to the user as progress.</summary>
public enum AnalysisStage { Queued, ReadingVacancy, Matching, Done }

public enum Importance { MustHave, NiceToHave }

public enum MatchStatus { Missing, Partial, Confirmed }

/// <summary>One comparison of a resume with a vacancy.</summary>
public class Analysis
{
    public Guid Id { get; set; }

    public Guid ResumeId { get; set; }
    public Resume Resume { get; set; } = null!;

    public Guid VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = null!;

    public AnalysisStatus Status { get; set; }
    public AnalysisStage Stage { get; set; }

    /// <summary>Which matcher produced the result, such as "keyword".</summary>
    public string? Engine { get; set; }

    /// <summary>Why the analysis failed, worded for the user. Null unless it failed.</summary>
    public string? Error { get; set; }

    /// <summary>Null until the analysis completes.</summary>
    public AnalysisResult? Result { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// The finished report. Stored as one JSON document: it is written once and always read
/// whole, so separate tables for requirements and evidence would add joins and no queries.
/// </summary>
public record AnalysisResult(int Score, IReadOnlyList<RequirementMatch> Requirements);

/// <summary>A requirement from the vacancy and the verdict on it.</summary>
/// <param name="Id">Stable within one analysis: "R1", "R2", and so on.</param>
/// <param name="VacancyLine">1-based line of the vacancy the requirement came from, when known.</param>
/// <param name="ResumeLines">1-based resume lines that support the verdict. Empty when missing.</param>
public record RequirementMatch(
    string Id,
    string Text,
    Importance Importance,
    int? VacancyLine,
    MatchStatus Status,
    string Reason,
    IReadOnlyList<int> ResumeLines);

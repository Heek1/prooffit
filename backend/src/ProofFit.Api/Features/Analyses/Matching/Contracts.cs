using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>A requirement as an extractor found it in the vacancy.</summary>
public record ExtractedRequirement(string Text, Importance Importance, int? VacancyLine);

/// <summary>An extracted requirement once the analysis has given it an id.</summary>
public record Requirement(string Id, string Text, Importance Importance, int? VacancyLine);

/// <summary>
/// A matcher's verdict before it has been checked. Matchers, the language model above all,
/// can cite lines that do not exist, so nothing here is trusted until
/// <see cref="EvidenceValidator"/> has confirmed the evidence.
/// </summary>
public record ProposedMatch(string RequirementId, MatchStatus Status, string Reason, IReadOnlyList<int> ResumeLines);

public interface IRequirementExtractor
{
    /// <summary>Finds what the vacancy asks of a candidate. Empty when it asks nothing recognisable.</summary>
    Task<IReadOnlyList<ExtractedRequirement>> ExtractAsync(NumberedText vacancy, CancellationToken ct);
}

public interface IRequirementMatcher
{
    /// <summary>Recorded on the analysis so a report says what produced it.</summary>
    string EngineName { get; }

    /// <summary>Judges each requirement against the resume. May omit requirements; they count as missing.</summary>
    Task<IReadOnlyList<ProposedMatch>> MatchAsync(
        IReadOnlyList<Requirement> requirements, NumberedText resume, CancellationToken ct);
}

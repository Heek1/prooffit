using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// Turns a matcher's proposals into verdicts the report can stand behind. This is the
/// guarantee the product makes: a requirement is never shown as met unless the lines it
/// cites really exist in the resume.
/// </summary>
public static class EvidenceValidator
{
    private const int MaxReasonLength = 300;

    public static IReadOnlyList<RequirementMatch> Validate(
        IReadOnlyList<Requirement> requirements, IReadOnlyList<ProposedMatch> proposed, NumberedText resume)
    {
        // A matcher may answer the same requirement twice or invent ids; the first answer
        // for a known id wins and the rest are ignored.
        var byRequirement = new Dictionary<string, ProposedMatch>(StringComparer.Ordinal);
        foreach (var match in proposed) byRequirement.TryAdd(match.RequirementId, match);

        return requirements.Select(requirement =>
        {
            if (!byRequirement.TryGetValue(requirement.Id, out var match))
                return Verdict(requirement, MatchStatus.Missing, "The matcher gave no verdict for this requirement.", []);

            if (match.Status == MatchStatus.Missing)
                return Verdict(requirement, MatchStatus.Missing, match.Reason, []);

            var lines = match.ResumeLines.Where(resume.HasContent).Distinct().Order().ToList();
            return lines.Count > 0
                ? Verdict(requirement, match.Status, match.Reason, lines)
                // The claim came with no evidence that checks out, so the claim is dropped
                // along with the reason that argued for it.
                : Verdict(requirement, MatchStatus.Missing, "No line in the resume supports this.", []);
        }).ToList();
    }

    private static RequirementMatch Verdict(
        Requirement requirement, MatchStatus status, string reason, IReadOnlyList<int> lines)
    {
        reason = reason.Trim();
        if (reason.Length == 0) reason = "No explanation was given.";
        if (reason.Length > MaxReasonLength) reason = reason[..MaxReasonLength].TrimEnd() + "…";

        return new RequirementMatch(
            requirement.Id, requirement.Text, requirement.Importance, requirement.VacancyLine, status, reason, lines);
    }
}

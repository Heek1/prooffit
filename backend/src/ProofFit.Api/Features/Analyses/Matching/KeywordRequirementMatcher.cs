using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// Matches by looking for the technologies a requirement names in the lines of the resume.
/// It cannot weigh experience, synonyms or context; that gap is what the model-based
/// matcher is for, and what the evaluation set measures.
/// </summary>
public class KeywordRequirementMatcher : IRequirementMatcher
{
    private const int MaxLinesPerSkill = 3;

    public string EngineName => "keyword";

    public Task<IReadOnlyList<ProposedMatch>> MatchAsync(
        IReadOnlyList<Requirement> requirements, NumberedText resume, CancellationToken ct)
    {
        var matches = requirements.Select(requirement => Match(requirement, resume)).ToList();
        return Task.FromResult<IReadOnlyList<ProposedMatch>>(matches);
    }

    private static ProposedMatch Match(Requirement requirement, NumberedText resume)
    {
        var skills = SkillCatalog.FindIn(requirement.Text);
        if (skills.Count == 0)
        {
            return new ProposedMatch(requirement.Id, MatchStatus.Missing,
                "This requirement names no technology the keyword matcher knows, so it could not be checked.", []);
        }

        var found = new List<string>();
        var absent = new List<string>();
        var lines = new List<int>();

        foreach (var skill in skills)
        {
            var mentions = Enumerable.Range(1, resume.Lines.Count)
                .Where(line => skill.IsIn(resume[line]))
                .Take(MaxLinesPerSkill)
                .ToList();

            (mentions.Count > 0 ? found : absent).Add(skill.Name);
            lines.AddRange(mentions);
        }

        var (status, reason) = (found.Count, absent.Count) switch
        {
            (_, 0) => (MatchStatus.Confirmed, $"The resume mentions {Join(found)}."),
            (0, _) => (MatchStatus.Missing, $"The resume does not mention {Join(absent)}."),
            _ => (MatchStatus.Partial, $"The resume mentions {Join(found)}, but not {Join(absent)}."),
        };
        return new ProposedMatch(requirement.Id, status, reason, lines);
    }

    private static string Join(IReadOnlyList<string> names) => names.Count switch
    {
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => string.Join(", ", names.Take(names.Count - 1)) + $" and {names[^1]}",
    };
}

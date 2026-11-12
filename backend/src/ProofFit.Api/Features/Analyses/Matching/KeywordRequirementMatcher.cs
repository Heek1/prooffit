using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// Matches by looking for the technologies a requirement names, in the lines of the resume
/// and among the candidate's GitHub facts. It cannot weigh experience, synonyms or context;
/// that gap is what the model-based matcher is for, and what the evaluation set measures.
/// </summary>
public class KeywordRequirementMatcher : IRequirementMatcher
{
    private const int MaxLinesPerSkill = 3;

    public string EngineName => "keyword";

    public Task<IReadOnlyList<ProposedMatch>> MatchAsync(
        IReadOnlyList<Requirement> requirements,
        NumberedText resume,
        IReadOnlyList<GitHubFact> gitHubFacts,
        CancellationToken ct)
    {
        var matches = requirements.Select(requirement => Match(requirement, resume, gitHubFacts)).ToList();
        return Task.FromResult<IReadOnlyList<ProposedMatch>>(matches);
    }

    private static ProposedMatch Match(Requirement requirement, NumberedText resume, IReadOnlyList<GitHubFact> gitHubFacts)
    {
        var skills = SkillCatalog.FindIn(requirement.Text);
        if (skills.Count == 0)
        {
            return new ProposedMatch(requirement.Id, MatchStatus.Missing,
                "This requirement names no technology the keyword matcher knows, so it could not be checked.");
        }

        var found = new List<string>();
        var absent = new List<string>();
        var lines = new List<int>();
        var facts = new List<string>();

        foreach (var skill in skills)
        {
            var mentions = Enumerable.Range(1, resume.Lines.Count)
                .Where(line => skill.IsIn(resume[line]))
                .Take(MaxLinesPerSkill)
                .ToList();
            var fact = gitHubFacts.FirstOrDefault(f => f.Skill == skill.Name);

            (mentions.Count > 0 || fact is not null ? found : absent).Add(skill.Name);
            lines.AddRange(mentions);
            if (fact is not null) facts.Add(fact.Id);
        }

        var (status, reason) = (found.Count, absent.Count) switch
        {
            (_, 0) => (MatchStatus.Confirmed, $"Found evidence of {Join(found)}."),
            (0, _) => (MatchStatus.Missing, $"Found no evidence of {Join(absent)}."),
            _ => (MatchStatus.Partial, $"Found evidence of {Join(found)}, but none of {Join(absent)}."),
        };
        return new ProposedMatch(requirement.Id, status, reason, lines, facts);
    }

    private static string Join(IReadOnlyList<string> names) => names.Count switch
    {
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => string.Join(", ", names.Take(names.Count - 1)) + $" and {names[^1]}",
    };
}

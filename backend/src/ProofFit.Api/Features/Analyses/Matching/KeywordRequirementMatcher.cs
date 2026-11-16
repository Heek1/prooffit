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
        var learning = new List<string>();
        var absent = new List<string>();
        var lines = new List<int>();
        var facts = new List<string>();

        foreach (var skill in skills)
        {
            // A line that names the skill but says the candidate has not used it is not
            // evidence; one that says they are learning it is weaker evidence.
            var mentions = Enumerable.Range(1, resume.Lines.Count)
                .Where(line => skill.IsIn(resume[line]))
                .Select(line => (Line: line, Use: LineContext.Classify(resume[line])))
                .Where(mention => mention.Use != LineUse.NotPractised)
                .Take(MaxLinesPerSkill)
                .ToList();
            var fact = gitHubFacts.FirstOrDefault(f => f.Skill == skill.Name);

            var practised = fact is not null || mentions.Any(mention => mention.Use == LineUse.Practised);
            (practised ? found : mentions.Count > 0 ? learning : absent).Add(skill.Name);
            lines.AddRange(mentions.Select(mention => mention.Line));
            if (fact is not null) facts.Add(fact.Id);
        }

        var (status, reason) = (found.Count, learning.Count, absent.Count) switch
        {
            (_, 0, 0) => (MatchStatus.Confirmed, $"Found evidence of {Join(found)}."),
            (0, 0, _) => (MatchStatus.Missing, $"Found no evidence of {Join(absent, "or")}."),
            _ => (MatchStatus.Partial, Partly(found, learning, absent)),
        };
        return new ProposedMatch(requirement.Id, status, reason, lines, facts);
    }

    private static string Partly(List<string> found, List<string> learning, List<string> absent)
    {
        var parts = new List<string>();
        if (found.Count > 0) parts.Add($"Found evidence of {Join(found)}");
        if (learning.Count > 0) parts.Add($"the resume says {Join(learning)} is still being learned");
        if (absent.Count > 0) parts.Add($"{(parts.Count > 0 ? "" : "found ")}none of {Join(absent, "or")}");

        var sentence = string.Join(found.Count > 0 ? ", but " : "; ", parts);
        return char.ToUpperInvariant(sentence[0]) + sentence[1..] + ".";
    }

    private static string Join(IReadOnlyList<string> names, string conjunction = "and") => names.Count switch
    {
        1 => names[0],
        2 => $"{names[0]} {conjunction} {names[1]}",
        _ => string.Join(", ", names.Take(names.Count - 1)) + $" {conjunction} {names[^1]}",
    };
}

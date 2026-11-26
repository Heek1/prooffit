using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// Matches by looking for the technologies a requirement names, in the lines of the resume
/// and among the candidate's GitHub facts. It cannot weigh experience, synonyms or context;
/// that gap is what the model-based matcher is for, and what the evaluation set measures.
/// </summary>
/// <param name="time">The clock that dates "to present"; the system clock when not given.</param>
public class KeywordRequirementMatcher(TimeProvider? time = null) : IRequirementMatcher
{
    private const int MaxLinesPerSkill = 3;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public string EngineName => "keyword";

    public Task<IReadOnlyList<ProposedMatch>> MatchAsync(
        IReadOnlyList<Requirement> requirements,
        NumberedText resume,
        IReadOnlyList<GitHubFact> gitHubFacts,
        CancellationToken ct)
    {
        var experience = Experience.Read(resume, DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime));
        var matches = requirements.Select(requirement => Match(requirement, resume, gitHubFacts, experience)).ToList();
        return Task.FromResult<IReadOnlyList<ProposedMatch>>(matches);
    }

    private static ProposedMatch Match(
        Requirement requirement, NumberedText resume, IReadOnlyList<GitHubFact> gitHubFacts, ExperienceSummary experience)
    {
        var years = Experience.YearsAsked(requirement.Text);
        var skills = SkillCatalog.FindIn(requirement.Text);
        if (skills.Count == 0 && years is not null && experience.Periods.Count > 0)
            return MatchTotalYears(requirement, years.Value, experience);
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

        // A requirement for years of a technology is checked against the dated jobs that
        // name it. A technology with no dated job behind it keeps its verdict: the resume
        // may state the years in words.
        if (years is not null && status != MatchStatus.Missing)
        {
            var asked = years.Value * 12;
            var dated = found.Where(skill => experience.MonthsWith(skill) > 0).ToList();
            var short_ = dated.Where(skill => experience.MonthsWith(skill) < asked).ToList();
            if (dated.Count > 0) lines = lines.Concat(dated.SelectMany(experience.PeriodsWith).SelectMany(period => period.Lines)).Distinct().Order().ToList();

            if (short_.Count > 0)
            {
                status = MatchStatus.Partial;
                reason = $"The resume's dates show {Join(short_.Select(skill => $"{Experience.Describe(experience.MonthsWith(skill))} of {skill}").ToList())}, where {Asked(years.Value)}.";
            }
            else if (dated.Count > 0 && status == MatchStatus.Confirmed)
            {
                reason = $"The resume's dates show {Join(dated.Select(skill => $"{Experience.Describe(experience.MonthsWith(skill))} of {skill}").ToList())}, where {Asked(years.Value)}.";
            }
        }

        return new ProposedMatch(requirement.Id, status, reason, lines, facts);
    }

    private static ProposedMatch MatchTotalYears(Requirement requirement, int years, ExperienceSummary experience)
    {
        var months = experience.TotalMonths;
        var status = months >= years * 12 ? MatchStatus.Confirmed : MatchStatus.Partial;
        return new ProposedMatch(requirement.Id, status,
            $"The resume's dates add up to {Experience.Describe(months)} of work, where {Asked(years)}.",
            experience.Periods.Select(period => period.Line).ToList());
    }

    private static string Asked(int years) => years == 1 ? "1 year is asked" : $"{years} years are asked";

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

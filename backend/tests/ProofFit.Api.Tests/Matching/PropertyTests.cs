using FsCheck;
using FsCheck.Fluent;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

/// <summary>
/// The guarantees the product makes, checked against many generated inputs rather than a
/// handful of chosen ones. The matcher is the part that cannot be trusted, so its proposals
/// are generated to be as wrong as a model can be: invented ids, repeated answers, line
/// numbers outside the resume, facts that were never gathered.
/// </summary>
public class PropertyTests
{
    private const int Runs = 500;

    private static readonly MatchStatus[] Statuses = [MatchStatus.Confirmed, MatchStatus.Partial, MatchStatus.Missing];
    private static readonly string[] GatheredFacts = ["G1", "G2", "G3"];

    /// <summary>A resume, the requirements, the facts gathered and whatever a matcher proposed.</summary>
    public record Scenario(
        string[] ResumeLines,
        Requirement[] Requirements,
        GitHubFact[] Facts,
        ProposedMatch[] Proposed)
    {
        public NumberedText Resume => new(string.Join('\n', ResumeLines));

        public IReadOnlyList<RequirementMatch> Validate() => EvidenceValidator.Validate(Requirements, Proposed, Resume, Facts);

        public override string ToString() =>
            $"resume [{string.Join(" | ", ResumeLines)}], requirements [{string.Join(", ", Requirements.Select(r => r.Id))}], " +
            $"proposed [{string.Join(", ", Proposed.Select(p => $"{p.RequirementId}:{p.Status}:L{string.Join('/', p.ResumeLines ?? [])}:F{string.Join('/', p.GitHubFacts ?? [])}"))}]";
    }

    private static readonly Gen<Scenario> Scenarios =
        from lineCount in Gen.Choose(1, 12)
        from lines in Gen.Elements("Built an API with ASP.NET Core", "", "   ", "Docker and Kubernetes", "React frontends").ListOf(lineCount)
        from requirementCount in Gen.Choose(0, 8)
        from importances in Gen.Elements(Importance.MustHave, Importance.NiceToHave).ListOf(requirementCount)
        from factCount in Gen.Choose(0, GatheredFacts.Length)
        from proposalCount in Gen.Choose(0, 12)
        from proposed in Proposal(requirementCount).ListOf(proposalCount)
        select new Scenario(
            lines.ToArray(),
            importances.Select((importance, index) => new Requirement($"R{index + 1}", $"Requirement {index + 1}", importance, null)).ToArray(),
            GatheredFacts.Take(factCount).Select(id => new GitHubFact(id, "Docker", [])).ToArray(),
            proposed.ToArray());

    // Ids beyond the real requirements and facts beyond the gathered ones are deliberate.
    private static Gen<ProposedMatch> Proposal(int requirementCount) =>
        from requirement in Gen.Choose(1, requirementCount + 3)
        from status in Gen.Elements(Statuses)
        from reason in Gen.Elements("", "   ", "Seen on line 2.", new string('x', 500))
        from lineCount in Gen.Choose(0, 4)
        from lines in Gen.Choose(-2, 15).ListOf(lineCount)
        from factCount in Gen.Choose(0, 3)
        from facts in Gen.Elements("G1", "G2", "G3", "G4", "G9").ListOf(factCount)
        select new ProposedMatch($"R{requirement}", status, reason, lines.ToArray(), facts.ToArray());

    private static void Check(Func<Scenario, bool> property) =>
        Prop.ForAll(Scenarios.ToArbitrary(), property).Check(Config.QuickThrowOnFailure.WithMaxTest(Runs));

    [Fact]
    public void Every_requirement_gets_exactly_one_verdict_in_its_own_order() => Check(scenario =>
        scenario.Validate().Select(match => match.Id).SequenceEqual(scenario.Requirements.Select(r => r.Id)));

    [Fact]
    public void Every_cited_line_exists_and_is_not_blank() => Check(scenario =>
    {
        var resume = scenario.Resume;
        return scenario.Validate().SelectMany(match => match.ResumeLines).All(resume.HasContent);
    });

    [Fact]
    public void Every_cited_fact_was_actually_gathered() => Check(scenario =>
    {
        var gathered = scenario.Facts.Select(fact => fact.Id).ToHashSet();
        return scenario.Validate().SelectMany(match => match.GitHubFacts).All(gathered.Contains);
    });

    [Fact]
    public void A_requirement_is_never_proven_or_partly_proven_without_evidence() => Check(scenario =>
        scenario.Validate()
            .Where(match => match.Status != MatchStatus.Missing)
            .All(match => match.ResumeLines.Count > 0 || match.GitHubFacts.Count > 0));

    [Fact]
    public void A_verdict_of_no_evidence_cites_nothing() => Check(scenario =>
        scenario.Validate()
            .Where(match => match.Status == MatchStatus.Missing)
            .All(match => match.ResumeLines.Count == 0 && match.GitHubFacts.Count == 0));

    [Fact]
    public void A_verdict_is_never_raised_above_what_the_matcher_proposed() => Check(scenario =>
    {
        // The validator may only take a claim away, never add one.
        var first = scenario.Proposed.GroupBy(p => p.RequirementId).ToDictionary(g => g.Key, g => g.First().Status);
        return scenario.Validate().All(match =>
            Rank(match.Status) <= Rank(first.TryGetValue(match.Id, out var proposed) ? proposed : MatchStatus.Missing));
    });

    [Fact]
    public void Every_reason_is_present_and_short() => Check(scenario =>
        scenario.Validate().All(match => match.Reason.Length is > 0 and <= 301));

    [Fact]
    public void The_score_stays_between_0_and_100() => Check(scenario =>
        Scoring.Score(scenario.Validate()) is >= 0 and <= 100);

    [Fact]
    public void Proving_one_more_requirement_never_lowers_the_score() => Check(scenario =>
    {
        var matches = scenario.Validate().ToList();
        var before = Scoring.Score(matches);
        return matches.Select((match, index) =>
        {
            var better = matches.ToList();
            better[index] = match with { Status = MatchStatus.Confirmed };
            return Scoring.Score(better) >= before;
        }).All(holds => holds);
    });

    [Fact]
    public void All_proven_scores_100_and_none_proven_scores_0() => Check(scenario =>
    {
        var matches = scenario.Validate();
        if (matches.Count == 0) return Scoring.Score(matches) == 0;
        return Scoring.Score(matches.Select(m => m with { Status = MatchStatus.Confirmed }).ToList()) == 100
            && Scoring.Score(matches.Select(m => m with { Status = MatchStatus.Missing }).ToList()) == 0;
    });

    private static int Rank(MatchStatus status) => status switch
    {
        MatchStatus.Confirmed => 2,
        MatchStatus.Partial => 1,
        _ => 0,
    };
}

using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class EvidenceValidatorTests
{
    // Line 2 is blank, so only lines 1 and 3 can be evidence.
    private static readonly NumberedText Resume = new("Built APIs with ASP.NET Core\n\nDocker and PostgreSQL");

    private static readonly Requirement Api = new("R1", "ASP.NET Core", Importance.MustHave, 4);
    private static readonly Requirement Containers = new("R2", "Docker", Importance.NiceToHave, null);

    [Fact]
    public void Keeps_a_verdict_whose_cited_lines_exist()
    {
        var match = Assert.Single(Validate([Api], new ProposedMatch("R1", MatchStatus.Confirmed, "Line 1 says so.", [1])));

        Assert.Equal(MatchStatus.Confirmed, match.Status);
        Assert.Equal([1], match.ResumeLines);
        Assert.Equal("Line 1 says so.", match.Reason);
        Assert.Equal(("ASP.NET Core", Importance.MustHave, 4), (match.Text, match.Importance, match.VacancyLine));
    }

    [Fact]
    public void Drops_cited_lines_that_are_blank_or_outside_the_resume()
    {
        var match = Assert.Single(Validate([Api],
            new ProposedMatch("R1", MatchStatus.Confirmed, "Evidence.", [3, 0, 2, 99, 1, 3])));

        Assert.Equal([1, 3], match.ResumeLines);
    }

    [Theory]
    [InlineData(MatchStatus.Confirmed)]
    [InlineData(MatchStatus.Partial)]
    public void Turns_a_positive_verdict_without_valid_evidence_into_missing(MatchStatus claimed)
    {
        var match = Assert.Single(Validate([Api],
            new ProposedMatch("R1", claimed, "The candidate clearly knows this.", [2, 42])));

        Assert.Equal(MatchStatus.Missing, match.Status);
        Assert.Empty(match.ResumeLines);
        Assert.Equal("No line in the resume or fact from GitHub supports this.", match.Reason);
    }

    [Fact]
    public void Shows_no_evidence_for_a_missing_verdict()
    {
        var match = Assert.Single(Validate([Api], new ProposedMatch("R1", MatchStatus.Missing, "Not mentioned.", [1])));

        Assert.Empty(match.ResumeLines);
        Assert.Equal("Not mentioned.", match.Reason);
    }

    [Fact]
    public void Marks_a_requirement_the_matcher_skipped_as_missing()
    {
        var matches = Validate([Api, Containers], new ProposedMatch("R2", MatchStatus.Confirmed, "Line 3.", [3]));

        Assert.Equal(["R1", "R2"], matches.Select(m => m.Id));
        Assert.Equal([MatchStatus.Missing, MatchStatus.Confirmed], matches.Select(m => m.Status));
    }

    [Fact]
    public void Ignores_verdicts_for_unknown_requirements_and_repeated_verdicts()
    {
        var matches = Validate([Api],
            new ProposedMatch("R9", MatchStatus.Confirmed, "Invented.", [1]),
            new ProposedMatch("R1", MatchStatus.Partial, "First answer.", [1]),
            new ProposedMatch("R1", MatchStatus.Confirmed, "Second answer.", [3]));

        var match = Assert.Single(matches);
        Assert.Equal((MatchStatus.Partial, "First answer."), (match.Status, match.Reason));
    }

    [Fact]
    public void Shortens_an_overlong_reason()
    {
        var match = Assert.Single(Validate([Api],
            new ProposedMatch("R1", MatchStatus.Confirmed, new string('x', 1000), [1])));

        Assert.Equal(301, match.Reason.Length);
        Assert.EndsWith("…", match.Reason);
    }

    [Fact]
    public void Accepts_a_github_fact_as_evidence_when_no_resume_line_is_cited()
    {
        var match = Assert.Single(Validate([Containers],
            new ProposedMatch("R2", MatchStatus.Confirmed, "Dockerfile in two repositories.", GitHubFacts: ["G1"])));

        Assert.Equal(MatchStatus.Confirmed, match.Status);
        Assert.Empty(match.ResumeLines);
        Assert.Equal(["G1"], match.GitHubFacts);
    }

    [Fact]
    public void Drops_github_facts_that_were_never_gathered()
    {
        var match = Assert.Single(Validate([Containers],
            new ProposedMatch("R2", MatchStatus.Confirmed, "Seen on GitHub.", [3], ["G7", "G1", "G1"])));

        Assert.Equal(["G1"], match.GitHubFacts);
        Assert.Equal([3], match.ResumeLines);
    }

    [Fact]
    public void Turns_a_verdict_resting_only_on_an_invented_github_fact_into_missing()
    {
        var match = Assert.Single(Validate([Containers],
            new ProposedMatch("R2", MatchStatus.Confirmed, "Seen on GitHub.", GitHubFacts: ["G7"])));

        Assert.Equal(MatchStatus.Missing, match.Status);
        Assert.Empty(match.GitHubFacts);
    }

    private static readonly GitHubFact[] Facts =
    [
        new("G1", "Docker", [new RepositoryRef("shop-api", "https://github.com/octo-dev/shop-api")]),
    ];

    private static IReadOnlyList<RequirementMatch> Validate(Requirement[] requirements, params ProposedMatch[] proposed) =>
        EvidenceValidator.Validate(requirements, proposed, Resume, Facts);
}

using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class KeywordRequirementMatcherTests
{
    private static readonly NumberedText Resume = new(string.Join('\n',
        "Vlad, full-stack developer",
        "Built a billing API with ASP.NET Core and Entity Framework Core",
        "React and TypeScript frontends",
        "PostgreSQL, Docker"));

    [Fact]
    public async Task Confirms_a_requirement_when_every_named_skill_is_in_the_resume()
    {
        var match = await Match("2+ years with ASP.NET Core and EF Core");

        Assert.Equal(MatchStatus.Confirmed, match.Status);
        Assert.Equal([2, 2], match.ResumeLines);
        Assert.Equal("The resume mentions ASP.NET Core and Entity Framework Core.", match.Reason);
    }

    [Fact]
    public async Task Reports_a_partial_match_and_names_what_is_absent()
    {
        var match = await Match("Docker and Kubernetes");

        Assert.Equal(MatchStatus.Partial, match.Status);
        Assert.Equal([4], match.ResumeLines);
        Assert.Equal("The resume mentions Docker, but not Kubernetes.", match.Reason);
    }

    [Fact]
    public async Task Reports_missing_when_no_named_skill_is_in_the_resume()
    {
        var match = await Match("Kafka");

        Assert.Equal(MatchStatus.Missing, match.Status);
        Assert.Empty(match.ResumeLines);
    }

    [Fact]
    public async Task Cannot_check_a_requirement_that_names_no_known_skill()
    {
        var match = await Match("Fluent spoken communication");

        Assert.Equal(MatchStatus.Missing, match.Status);
        Assert.Contains("could not be checked", match.Reason);
    }

    private static async Task<ProposedMatch> Match(string requirement)
    {
        var matches = await new KeywordRequirementMatcher().MatchAsync(
            [new Requirement("R1", requirement, Importance.MustHave, null)], Resume, CancellationToken.None);
        return Assert.Single(matches);
    }
}

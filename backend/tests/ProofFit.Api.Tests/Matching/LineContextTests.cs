using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class LineContextTests
{
    [Theory]
    [InlineData("No production experience with Kubernetes yet")]
    [InlineData("Without commercial experience in React")]
    [InlineData("Have not used Kafka")]
    [InlineData("Never worked with Angular")]
    [InlineData("Lacking hands-on experience with AWS")]
    [InlineData("Worked alongside developers who used React and Docker")]
    [InlineData("Supported engineers who built the Kafka pipeline")]
    public void Reads_a_mention_that_is_not_the_candidates_own_work(string line)
    {
        Assert.Equal(LineUse.NotPractised, LineContext.Classify(line));
    }

    [Theory]
    [InlineData("Currently learning Go in my spare time")]
    [InlineData("Studying Rust")]
    [InlineData("Completed an online course on Kubernetes")]
    public void Reads_a_mention_of_something_being_learned(string line)
    {
        Assert.Equal(LineUse.Learning, LineContext.Classify(line));
    }

    [Theory]
    [InlineData("Built a payments API with ASP.NET Core")]
    [InlineData("Deployed with no downtime using Docker")]
    [InlineData("Led a team that shipped a React app")]
    [InlineData("Mentored engineers on PostgreSQL tuning")]
    public void Reads_ordinary_resume_lines_as_practised(string line)
    {
        Assert.Equal(LineUse.Practised, LineContext.Classify(line));
    }
}

public class KeywordMatcherContextTests
{
    private static async Task<ProposedMatch> Match(string requirement, params string[] resumeLines)
    {
        var matches = await new KeywordRequirementMatcher().MatchAsync(
            [new Requirement("R1", requirement, Importance.MustHave, null)],
            new NumberedText(string.Join('\n', resumeLines)),
            [],
            CancellationToken.None);
        return Assert.Single(matches);
    }

    [Fact]
    public async Task Does_not_count_a_negated_mention_as_evidence()
    {
        var match = await Match("Kubernetes", "Platform engineer", "No production experience with Kubernetes yet");

        Assert.Equal(MatchStatus.Missing, match.Status);
        Assert.Empty(match.ResumeLines!);
    }

    [Fact]
    public async Task Does_not_count_other_peoples_work_as_evidence()
    {
        var match = await Match("React", "Marketing manager", "Worked alongside developers who used React");

        Assert.Equal(MatchStatus.Missing, match.Status);
    }

    [Fact]
    public async Task Counts_a_skill_being_learned_as_partial_and_cites_the_line()
    {
        var match = await Match("Kubernetes", "Backend developer", "Studying Kubernetes");

        Assert.Equal(MatchStatus.Partial, match.Status);
        Assert.Equal([2], match.ResumeLines);
        Assert.Equal("The resume says Kubernetes is still being learned.", match.Reason);
    }

    [Fact]
    public async Task Prefers_a_line_of_real_use_over_a_negated_one()
    {
        var match = await Match("Docker", "No experience with Docker at my first job", "Containerised three services with Docker");

        Assert.Equal(MatchStatus.Confirmed, match.Status);
        Assert.Equal([2], match.ResumeLines);
    }
}

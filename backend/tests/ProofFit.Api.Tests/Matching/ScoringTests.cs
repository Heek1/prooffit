using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class ScoringTests
{
    [Fact]
    public void Scores_100_when_everything_is_confirmed()
    {
        Assert.Equal(100, Scoring.Score(
        [
            Match(Importance.MustHave, MatchStatus.Confirmed),
            Match(Importance.NiceToHave, MatchStatus.Confirmed),
        ]));
    }

    [Fact]
    public void Scores_0_when_nothing_is_found_or_there_is_nothing_to_score()
    {
        Assert.Equal(0, Scoring.Score([Match(Importance.MustHave, MatchStatus.Missing)]));
        Assert.Equal(0, Scoring.Score([]));
    }

    [Fact]
    public void Counts_a_must_have_twice_as_much_as_a_nice_to_have()
    {
        // Earned 2 of 3 with the must-have confirmed, 1 of 3 with only the nice-to-have.
        Assert.Equal(67, Scoring.Score(
        [
            Match(Importance.MustHave, MatchStatus.Confirmed),
            Match(Importance.NiceToHave, MatchStatus.Missing),
        ]));
        Assert.Equal(33, Scoring.Score(
        [
            Match(Importance.MustHave, MatchStatus.Missing),
            Match(Importance.NiceToHave, MatchStatus.Confirmed),
        ]));
    }

    [Fact]
    public void Gives_half_credit_for_a_partial_match()
    {
        Assert.Equal(50, Scoring.Score([Match(Importance.MustHave, MatchStatus.Partial)]));
    }

    private static RequirementMatch Match(Importance importance, MatchStatus status) =>
        new("R1", "requirement", importance, null, status, "reason", []);
}

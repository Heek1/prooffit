using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// The score is arithmetic over the verdicts, never something a model is asked for, so the
/// same verdicts always give the same number and the number can be explained.
/// </summary>
public static class Scoring
{
    /// <summary>A must-have counts twice as much as a nice-to-have.</summary>
    public static int Weight(Importance importance) => importance == Importance.MustHave ? 2 : 1;

    /// <summary>How much of a requirement's weight a verdict earns.</summary>
    public static double Credit(MatchStatus status) => status switch
    {
        MatchStatus.Confirmed => 1,
        MatchStatus.Partial => 0.5,
        _ => 0,
    };

    /// <summary>Weighted share of requirements backed by evidence, from 0 to 100.</summary>
    public static int Score(IReadOnlyList<RequirementMatch> matches)
    {
        var total = matches.Sum(match => Weight(match.Importance));
        if (total == 0) return 0;

        var earned = matches.Sum(match => Weight(match.Importance) * Credit(match.Status));
        return (int)Math.Round(100 * earned / total, MidpointRounding.AwayFromZero);
    }
}

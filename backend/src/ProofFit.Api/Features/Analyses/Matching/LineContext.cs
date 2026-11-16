using System.Text.RegularExpressions;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>What a resume line says about the candidate's own use of what it mentions.</summary>
public enum LineUse
{
    /// <summary>The line describes the candidate's own work, as most resume lines do.</summary>
    Practised,

    /// <summary>The candidate is studying it: a course, a tutorial, "currently learning".</summary>
    Learning,

    /// <summary>The line names it without the candidate having used it: a negation, or other people's work.</summary>
    NotPractised,
}

/// <summary>
/// Reads the few phrasings that turn a mention into something other than evidence. A word
/// list cannot judge a resume the way a model can; it is here so that the keyword engine
/// stops counting the plainest cases, "no experience with X" and "worked alongside people
/// who used X", as proof that the candidate knows X.
/// </summary>
public static partial class LineContext
{
    public static LineUse Classify(string line)
    {
        if (Negation().IsMatch(line) || OthersWork().IsMatch(line)) return LineUse.NotPractised;
        if (Study().IsMatch(line)) return LineUse.Learning;
        return LineUse.Practised;
    }

    // Experience that the line itself says is missing. Plain "no" is not enough on its own:
    // "deployed with no downtime using Docker" is still Docker experience.
    [GeneratedRegex(
        @"\b(?:no|not\s+any|without)\s+(?:[\w-]+\s+){0,2}(?:experience|exposure|knowledge)\b|\b(?:never|not|haven't|have\s+not|hasn't)\s+(?:yet\s+)?(?:used|worked\s+with|tried)\b|\black(?:s|ing)?\s+(?:[\w-]+\s+){0,2}(?:experience|knowledge)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Negation();

    // Work the line attributes to someone else.
    [GeneratedRegex(
        @"\b(?:alongside|together\s+with)\s+(?:\w+\s+){0,3}(?:developers|engineers|colleagues|teams?)\b|\b(?:developers|engineers|colleagues|teammates|people|others)\s+who\s+(?:used|built|wrote|ran)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OthersWork();

    [GeneratedRegex(
        @"\b(?:currently\s+)?(?:learning|studying)\b|\b(?:introductory|online|beginner)\s+course\b|\btutorials?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Study();
}

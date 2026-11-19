using System.Text.RegularExpressions;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>A dated stretch of work in a resume, with the technologies its lines name.</summary>
/// <param name="Line">The resume line that holds the dates.</param>
/// <param name="FirstMonth">The first month worked, counted as year × 12 + month − 1.</param>
/// <param name="LastMonth">The last month worked, counted the same way.</param>
/// <param name="Lines">The lines that describe this work: the dated line and those under it.</param>
public record WorkPeriod(int Line, int FirstMonth, int LastMonth, IReadOnlyList<int> Lines, IReadOnlyList<string> Skills)
{
    public int Months => LastMonth - FirstMonth + 1;
}

/// <summary>
/// Years of experience counted from the dates in a resume. Overlapping jobs are counted
/// once, so two part-time jobs in the same year make one year, not two.
/// </summary>
public class ExperienceSummary(IReadOnlyList<WorkPeriod> periods)
{
    public IReadOnlyList<WorkPeriod> Periods { get; } = periods;

    public int TotalMonths => CountMonths(Periods);

    public int MonthsWith(string skill) => CountMonths(PeriodsWith(skill));

    public IReadOnlyList<WorkPeriod> PeriodsWith(string skill) => Periods.Where(period => period.Skills.Contains(skill)).ToList();

    private static int CountMonths(IEnumerable<WorkPeriod> periods) =>
        periods.SelectMany(period => Enumerable.Range(period.FirstMonth, period.Months)).Distinct().Count();
}

/// <summary>
/// Reads work periods from a resume and amounts of experience from a requirement. Date
/// arithmetic is where a language model is least reliable and code is exact, so the model
/// is handed these numbers rather than asked to work them out.
/// </summary>
public static partial class Experience
{
    // A job is described by its dated line and the lines under it, up to the next dated
    // line or a blank one. Longer runs are more likely another section than the same job.
    private const int MaxLinesPerPeriod = 8;

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["feb"] = 2, ["mar"] = 3, ["apr"] = 4, ["may"] = 5, ["jun"] = 6,
        ["jul"] = 7, ["aug"] = 8, ["sep"] = 9, ["oct"] = 10, ["nov"] = 11, ["dec"] = 12,
        ["січ"] = 1, ["лют"] = 2, ["бер"] = 3, ["кві"] = 4, ["тра"] = 5, ["чер"] = 6,
        ["лип"] = 7, ["сер"] = 8, ["вер"] = 9, ["жов"] = 10, ["лис"] = 11, ["гру"] = 12,
    };

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
    };

    public static ExperienceSummary Read(NumberedText resume, DateOnly today)
    {
        var now = today.Year * 12 + today.Month - 1;
        var dated = new List<(int Line, int First, int Last)>();
        for (var line = 1; line <= resume.Lines.Count; line++)
        {
            var text = resume[line];
            if (Education().IsMatch(text) || LineContext.Classify(text) != LineUse.Practised) continue;
            if (Period().Match(text) is not { Success: true } match) continue;

            var first = MonthIndex(match.Groups["y1"].Value, match.Groups["m1"].Value, match.Groups["n1"].Value, startOfYear: true);
            var last = match.Groups["now"].Success
                ? now
                : MonthIndex(match.Groups["y2"].Value, match.Groups["m2"].Value, match.Groups["n2"].Value, startOfYear: false);

            // A period that ends before it starts, or in the future, is a misreading.
            if (first is null || last is null || first > last || last > now) continue;
            dated.Add((line, first.Value, last.Value));
        }

        var periods = new List<WorkPeriod>();
        for (var index = 0; index < dated.Count; index++)
        {
            var (line, first, last) = dated[index];
            var end = Math.Min(index + 1 < dated.Count ? dated[index + 1].Line - 1 : resume.Lines.Count, line + MaxLinesPerPeriod - 1);
            var lines = new List<int> { line };
            for (var next = line + 1; next <= end && resume[next].Trim().Length > 0; next++) lines.Add(next);

            var skills = lines
                .Where(number => LineContext.Classify(resume[number]) == LineUse.Practised)
                .SelectMany(number => SkillCatalog.FindIn(resume[number]))
                .Select(skill => skill.Name)
                .Distinct()
                .ToList();
            periods.Add(new WorkPeriod(line, first, last, lines, skills));
        }

        return new ExperienceSummary(periods);
    }

    /// <summary>The years a requirement asks for, such as 3 in "3+ years of Python"; null when it names none.</summary>
    public static int? YearsAsked(string requirement)
    {
        if (YearsPhrase().Match(requirement) is not { Success: true } match) return null;
        var amount = match.Groups["n"].Value;
        return int.TryParse(amount, out var years) ? years : NumberWords.GetValueOrDefault(amount) is > 0 and var word ? word : null;
    }

    /// <summary>"2 years 3 months", "11 months", "4 years".</summary>
    public static string Describe(int months)
    {
        var (years, rest) = (months / 12, months % 12);
        var parts = new List<string>();
        if (years > 0) parts.Add(years == 1 ? "1 year" : $"{years} years");
        if (rest > 0 || years == 0) parts.Add(rest == 1 ? "1 month" : $"{rest} months");
        return string.Join(' ', parts);
    }

    // A start without a month counts from January; an end without one runs to December, so
    // "2017 to 2018" is two years, as a reader would take it.
    private static int? MonthIndex(string year, string monthName, string monthNumber, bool startOfYear)
    {
        if (!int.TryParse(year, out var y)) return null;
        int month;
        if (monthNumber.Length > 0) month = int.Parse(monthNumber);
        else if (monthName.Length >= 3 && Months.TryGetValue(monthName[..3], out var named)) month = named;
        else month = startOfYear ? 1 : 12;
        return month is >= 1 and <= 12 ? y * 12 + month - 1 : null;
    }

    // "March 2019 to February 2022", "Jan 2020 – present", "03/2019 - 02/2022", "2017 to 2018",
    // "вересень 2020 – дотепер". A word before a year that is not a month is ignored.
    [GeneratedRegex(
        @"(?:(?<m1>\p{L}{3,10})\.?\s+|(?<n1>\d{1,2})[./])?(?<y1>(?:19|20)\d{2})\s*(?:-|–|—|to|until|till|до|по)\s*(?:(?:(?<m2>\p{L}{3,10})\.?\s+|(?<n2>\d{1,2})[./])?(?<y2>(?:19|20)\d{2})\b|(?<now>present|now|current|today|date|дотепер|тепер\p{L}*|зараз|сьогодні)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Period();

    // Study, not work: degrees and schools carry dates too.
    [GeneratedRegex(
        @"\b(?:university|college|school|academy|bachelor|master|b\.?sc|m\.?sc|ph\.?d|degree|diploma|bootcamp|course|університет|інститут|академі\p{L}*|бакалавр|магістр|коледж|курс\p{L}*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Education();

    // "3+ years", "at least 2 years", "five years", "від 2 років".
    [GeneratedRegex(
        @"(?<n>\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten)\s*\+?\s*(?:years?|yrs?|рок\p{L}*|рік|літ)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YearsPhrase();
}

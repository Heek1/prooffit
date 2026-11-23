using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

// Cases found by mutation testing (Stryker.NET): changes to the code that no earlier test
// noticed, such as ">" becoming ">=" at a limit or a month name being dropped.

public class EvidenceValidatorBoundaryTests
{
    private static readonly Requirement Api = new("R1", "ASP.NET Core", Importance.MustHave, 1);
    private static readonly NumberedText Resume = new("Built APIs with ASP.NET Core");

    private static RequirementMatch Validate(params ProposedMatch[] proposed) =>
        Assert.Single(EvidenceValidator.Validate([Api], proposed, Resume, []));

    [Fact]
    public void Says_so_when_the_matcher_gave_no_verdict()
    {
        var match = Validate();

        Assert.Equal((MatchStatus.Missing, "The matcher gave no verdict for this requirement."), (match.Status, match.Reason));
    }

    [Fact]
    public void Fills_in_a_reason_the_matcher_left_out()
    {
        Assert.Equal("No explanation was given.", Validate(new ProposedMatch("R1", MatchStatus.Confirmed, null!, [1])).Reason);
    }

    [Fact]
    public void Keeps_a_reason_of_exactly_the_maximum_length_whole()
    {
        var reason = new string('a', 300);

        Assert.Equal(reason, Validate(new ProposedMatch("R1", MatchStatus.Confirmed, reason, [1])).Reason);
    }

    [Fact]
    public void Shortens_a_longer_reason_and_marks_the_cut()
    {
        var reason = new string('a', 301);

        Assert.Equal(new string('a', 300) + "…", Validate(new ProposedMatch("R1", MatchStatus.Confirmed, reason, [1])).Reason);
    }
}

public class ExperienceBoundaryTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static int Months(string line) => Experience.Read(new NumberedText(line), Today).TotalMonths;

    // January to the named month of one year: its number of months is the month's number.
    [Theory]
    [InlineData("January", 1)] [InlineData("February", 2)] [InlineData("March", 3)] [InlineData("April", 4)]
    [InlineData("May", 5)] [InlineData("June", 6)] [InlineData("July", 7)] [InlineData("August", 8)]
    [InlineData("September", 9)] [InlineData("October", 10)] [InlineData("November", 11)] [InlineData("December", 12)]
    public void Reads_every_english_month(string month, int number)
    {
        Assert.Equal(number, Months($"Acme, Python, Jan 2020 to {month} 2020"));
    }

    [Theory]
    [InlineData("січень", 1)] [InlineData("лютий", 2)] [InlineData("березень", 3)] [InlineData("квітень", 4)]
    [InlineData("травень", 5)] [InlineData("червень", 6)] [InlineData("липень", 7)] [InlineData("серпень", 8)]
    [InlineData("вересень", 9)] [InlineData("жовтень", 10)] [InlineData("листопад", 11)] [InlineData("грудень", 12)]
    public void Reads_every_ukrainian_month(string month, int number)
    {
        Assert.Equal(number, Months($"Acme, Python, січень 2020 – {month} 2020"));
    }

    [Theory]
    [InlineData("one", 1)] [InlineData("two", 2)] [InlineData("three", 3)] [InlineData("four", 4)] [InlineData("five", 5)]
    [InlineData("six", 6)] [InlineData("seven", 7)] [InlineData("eight", 8)] [InlineData("nine", 9)] [InlineData("ten", 10)]
    public void Reads_years_written_as_a_word(string word, int years)
    {
        Assert.Equal(years, Experience.YearsAsked($"{word} years of Python"));
    }

    [Theory]
    [InlineData("Acme, Python, 13/2020 - 02/2021")]
    [InlineData("Acme, Python, 00/2020 - 02/2021")]
    public void Ignores_a_month_number_that_is_not_a_month(string line)
    {
        Assert.Empty(Experience.Read(new NumberedText(line), Today).Periods);
    }
}

public class KeywordMatcherBoundaryTests
{
    private static readonly TimeProvider Clock = new FixedTime(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    private static async Task<ProposedMatch> Match(string requirement, params string[] resume)
    {
        var matches = await new KeywordRequirementMatcher(Clock).MatchAsync(
            [new Requirement("R1", requirement, Importance.MustHave, null)], new NumberedText(string.Join('\n', resume)), [], CancellationToken.None);
        return Assert.Single(matches);
    }

    [Fact]
    public void Is_named_keyword()
    {
        Assert.Equal("keyword", new KeywordRequirementMatcher().EngineName);
    }

    [Fact]
    public async Task Confirms_exactly_the_years_asked_for_a_technology()
    {
        var match = await Match("3+ years of Python", "Acme, Python developer, January 2021 to December 2023");

        Assert.Equal(MatchStatus.Confirmed, match.Status);
    }

    [Fact]
    public async Task Confirms_exactly_the_years_asked_in_total()
    {
        var match = await Match("3+ years of commercial experience", "Acme, Python developer, January 2021 to December 2023");

        Assert.Equal(MatchStatus.Confirmed, match.Status);
    }

    [Fact]
    public async Task Words_a_single_year_as_one()
    {
        var match = await Match("1 year with Docker", "Acme, Docker in production, June 2026 to present");

        Assert.Equal("The resume's dates show 6 months of Docker, where 1 year is asked.", match.Reason);
    }

    [Fact]
    public async Task Without_dates_a_requirement_for_years_alone_cannot_be_checked()
    {
        var match = await Match("5+ years of commercial experience", "Backend developer");

        Assert.Equal(MatchStatus.Missing, match.Status);
        Assert.StartsWith("This requirement names no technology", match.Reason);
    }

    [Fact]
    public async Task Says_what_is_being_learned_and_what_is_absent()
    {
        var match = await Match("Kafka and Redis", "Currently learning Kafka");

        Assert.Equal((MatchStatus.Partial, "The resume says Kafka is still being learned; none of Redis."), (match.Status, match.Reason));
    }

    [Fact]
    public async Task Lists_three_absent_technologies_with_commas()
    {
        var match = await Match("Docker, Kubernetes and Kafka", "Frontend developer");

        // Named in the skill catalog's order, not the requirement's.
        Assert.Equal("Found no evidence of Kafka, Docker or Kubernetes.", match.Reason);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

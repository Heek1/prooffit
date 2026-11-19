using ProofFit.Api.Tests.Llm;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class ExperienceTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static ExperienceSummary Read(params string[] lines) =>
        Experience.Read(new NumberedText(string.Join('\n', lines)), Today);

    [Theory]
    [InlineData("Acme, Python developer, March 2019 to February 2022", 36)]
    [InlineData("Acme, Jan 2020 – Mar 2021, Python", 15)]
    [InlineData("Acme, 03/2019 - 02/2022, Python", 36)]
    [InlineData("Freelance Python work, 2017 to 2018", 24)]
    [InlineData("Acme, Python developer, 2024 – present", 35)]
    [InlineData("Acme, Python developer, Sept 2025 - now", 15)]
    [InlineData("Acme, розробник Python, вересень 2025 – дотепер", 15)]
    public void Counts_the_months_of_a_dated_job(string line, int months)
    {
        var experience = Read(line);

        Assert.Equal(months, experience.TotalMonths);
        Assert.Equal(months, experience.MonthsWith("Python"));
    }

    [Fact]
    public void Credits_a_job_with_the_technologies_on_the_lines_under_it()
    {
        var experience = Read(
            "Senior developer, Acme",
            "Jan 2020 – Dec 2021",
            "- Built services with Docker",
            "- Ran PostgreSQL",
            "",
            "Developer, Beta, Jan 2022 – Dec 2022",
            "- React frontends");

        Assert.Equal(24, experience.MonthsWith("Docker"));
        Assert.Equal(24, experience.MonthsWith("PostgreSQL"));
        Assert.Equal(12, experience.MonthsWith("React"));
        Assert.Equal(36, experience.TotalMonths);
        Assert.Equal([2, 3, 4], experience.Periods[0].Lines);
    }

    [Fact]
    public void Counts_overlapping_jobs_once()
    {
        var experience = Read(
            "Acme, Python, Jan 2020 to Dec 2021",
            "Beta, part-time Python, Jan 2021 to Dec 2022");

        Assert.Equal(36, experience.TotalMonths);
        Assert.Equal(36, experience.MonthsWith("Python"));
    }

    [Fact]
    public void Leaves_out_study_and_lines_that_are_not_the_candidates_work()
    {
        var experience = Read(
            "BSc Computer Science, Kyiv University, 2014 to 2018",
            "Online course in React, 2023 to 2024",
            "Worked alongside developers who used Java, 2019 to 2022");

        Assert.Empty(experience.Periods);
    }

    [Fact]
    public void Ignores_a_period_that_ends_before_it_starts_or_in_the_future()
    {
        Assert.Empty(Read("Acme, 2022 to 2019", "Beta, 2025 to 2030").Periods);
    }

    [Theory]
    [InlineData("3+ years of Python", 3)]
    [InlineData("At least 2 years with React", 2)]
    [InlineData("Five years of commercial experience", 5)]
    [InlineData("Досвід роботи з Node.js від 2 років", 2)]
    [InlineData("1 year with Docker", 1)]
    [InlineData("Python", null)]
    [InlineData("Python 3.12", null)]
    public void Reads_the_years_a_requirement_asks_for(string requirement, int? years)
    {
        Assert.Equal(years, Experience.YearsAsked(requirement));
    }

    [Theory]
    [InlineData(1, "1 month")]
    [InlineData(11, "11 months")]
    [InlineData(12, "1 year")]
    [InlineData(27, "2 years 3 months")]
    [InlineData(48, "4 years")]
    public void Describes_a_number_of_months(int months, string text)
    {
        Assert.Equal(text, Experience.Describe(months));
    }
}

public class YearsInMatchingTests
{
    private static readonly TimeProvider Clock = new FixedTime(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    private static readonly NumberedText Resume = new(string.Join('\n',
        "Backend developer",
        "Acme Corp, Python developer, March 2019 to February 2022",
        "Beta Ltd, Python developer, March 2022 to present",
        "Freelance C# work, 2017 to 2018"));

    private static async Task<ProposedMatch> Match(string requirement)
    {
        var requirements = new[] { new Requirement("R1", requirement, Importance.MustHave, 1) };
        var matches = await new KeywordRequirementMatcher(Clock).MatchAsync(requirements, Resume, [], CancellationToken.None);
        return Assert.Single(matches);
    }

    [Fact]
    public async Task Confirms_years_the_dates_add_up_to()
    {
        var match = await Match("5+ years of Python");

        Assert.Equal(MatchStatus.Confirmed, match.Status);
        Assert.Equal("The resume's dates show 7 years 9 months of Python, where 5 years are asked.", match.Reason);
        Assert.Equal([2, 3], match.ResumeLines);
    }

    [Fact]
    public async Task Reports_fewer_years_than_asked_as_partial()
    {
        var match = await Match("3+ years of C#");

        Assert.Equal(MatchStatus.Partial, match.Status);
        Assert.Equal("The resume's dates show 2 years of C#, where 3 years are asked.", match.Reason);
        Assert.Equal([4], match.ResumeLines);
    }

    [Fact]
    public async Task Counts_total_experience_when_no_technology_is_named()
    {
        var match = await Match("10+ years of commercial experience");

        Assert.Equal(MatchStatus.Partial, match.Status);
        Assert.Equal("The resume's dates add up to 9 years 9 months of work, where 10 years are asked.", match.Reason);
        Assert.Equal([2, 3, 4], match.ResumeLines);
    }

    [Fact]
    public async Task Keeps_the_verdict_when_no_dated_job_names_the_technology()
    {
        var requirements = new[] { new Requirement("R1", "3+ years of Django", Importance.MustHave, 1) };
        var resume = new NumberedText("Three years of commercial experience with Django");

        var match = Assert.Single(await new KeywordRequirementMatcher(Clock).MatchAsync(requirements, resume, [], CancellationToken.None));

        Assert.Equal(MatchStatus.Confirmed, match.Status);
    }

    [Fact]
    public async Task Gives_the_model_the_counted_years()
    {
        var llm = new FakeLlmClient("""{ "matches": [{ "requirementId": "R1", "status": "missing", "reason": "x" }] }""");
        var requirements = new[] { new Requirement("R1", "5+ years of Python", Importance.MustHave, 1) };

        await new LlmRequirementMatcher(llm, Clock).MatchAsync(requirements, Resume, [], CancellationToken.None);

        var prompt = Assert.Single(llm.Prompts).User;
        Assert.Contains(
            "EXPERIENCE COUNTED FROM THE RESUME'S DATES\n" +
            "Total: 9 years 9 months, from lines 2, 3, 4\n" +
            "Python: 7 years 9 months, from lines 2, 3\n" +
            "C#: 2 years, from line 4\n", prompt);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

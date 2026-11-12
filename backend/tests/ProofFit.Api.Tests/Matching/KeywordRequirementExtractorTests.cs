using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class KeywordRequirementExtractorTests
{
    private readonly KeywordRequirementExtractor _extractor = new();

    [Fact]
    public async Task Reads_requirements_from_headed_bullet_lists()
    {
        var requirements = await Extract("""
            We are hiring a full-stack developer.

            Responsibilities:
            - Build features with React
            - Review code

            Requirements:
            - 2+ years with ASP.NET Core and EF Core
            - Fluent spoken communication

            Nice to have:
            - Angular
            """);

        Assert.Equal(
        [
            new("2+ years with ASP.NET Core and EF Core", Importance.MustHave, 8),
            new("Fluent spoken communication", Importance.MustHave, 9),
            new("Angular", Importance.NiceToHave, 12),
        ], requirements);
    }

    [Fact]
    public async Task Treats_a_line_marked_as_a_plus_as_nice_to_have()
    {
        var requirements = await Extract("""
            Requirements:
            - Docker
            - Kubernetes is a plus
            """);

        Assert.Equal([Importance.MustHave, Importance.NiceToHave], requirements.Select(r => r.Importance));
    }

    [Fact]
    public async Task Picks_up_lines_that_name_a_skill_when_there_are_no_headings()
    {
        var requirements = await Extract("""
            You will join a small product team.
            We work with Python and FastAPI.
            Lunch is on us.
            """);

        Assert.Equal([new("We work with Python and FastAPI.", Importance.MustHave, 2)], requirements);
    }

    [Fact]
    public async Task Reads_ukrainian_headings()
    {
        var requirements = await Extract("""
            Вимоги:
            • Досвід з React
            Буде плюсом:
            • Docker
            Ми пропонуємо:
            • Курси Python
            """);

        Assert.Equal(
        [
            new("Досвід з React", Importance.MustHave, 2),
            new("Docker", Importance.NiceToHave, 4),
        ], requirements);
    }

    [Fact]
    public async Task Keeps_a_short_line_that_lists_skills_instead_of_reading_it_as_a_heading()
    {
        var requirements = await Extract("Tech stack: React, Node.js, PostgreSQL");

        Assert.Single(requirements);
    }

    [Fact]
    public async Task Lists_a_repeated_requirement_once()
    {
        var requirements = await Extract("""
            Requirements:
            - Docker
            - docker
            """);

        Assert.Single(requirements);
    }

    [Fact]
    public async Task Finds_nothing_in_text_without_requirements()
    {
        Assert.Empty(await Extract("We are a friendly company looking for a great person."));
    }

    private Task<IReadOnlyList<ExtractedRequirement>> Extract(string vacancy) =>
        _extractor.ExtractAsync(new NumberedText(vacancy.ReplaceLineEndings("\n")), CancellationToken.None);
}

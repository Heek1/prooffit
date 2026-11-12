using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Tests.Matching;

public class SkillCatalogTests
{
    [Theory]
    [InlineData("Built APIs with ASP.NET Core", "ASP.NET Core")]
    [InlineData("react.js and Redux", "React", "Redux")]
    [InlineData("Postgres, docker compose, GitHub Actions", "PostgreSQL", "Docker", "CI/CD")]
    [InlineData("C#/.NET developer", "C#", ".NET")]
    [InlineData("Integrated the Gemini API with a RAG pipeline", "LLM APIs", "RAG")]
    public void Finds_skills_under_any_of_their_spellings(string text, params string[] expected)
    {
        Assert.Equal(expected, Names(text));
    }

    [Theory]
    [InlineData("JavaScript")]            // not Java
    [InlineData("ASP.NET Core")]          // not .NET on its own
    [InlineData("C++ and C#")]            // neither is plain "C"
    [InlineData("Expressive, reactive UIs")]
    public void Does_not_match_inside_a_longer_word(string text)
    {
        Assert.DoesNotContain("Java", Names(text));
        Assert.DoesNotContain(".NET", Names(text));
        Assert.DoesNotContain("React", Names(text));
    }

    [Fact]
    public void Matches_capitalised_aliases_only_in_capitals()
    {
        Assert.Contains("REST APIs", Names("Designed REST endpoints"));
        Assert.DoesNotContain("REST APIs", Names("and the rest of the team"));
    }

    private static string[] Names(string text) => SkillCatalog.FindIn(text).Select(skill => skill.Name).ToArray();
}

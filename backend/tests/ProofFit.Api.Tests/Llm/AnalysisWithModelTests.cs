using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Llm;

namespace ProofFit.Api.Tests.Llm;

/// <summary>Analyses with a model configured: the same API and database, with Gemini replaced by a stub.</summary>
[Collection(ApiCollection.Name)]
public class AnalysisWithModelTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Kubernetes in production
        - Team leadership is a plus
        - Docker
        """;

    private static readonly string[] Resume = ["Platform engineer", "Containerised services with Docker"];

    private readonly StubGemini _gemini = new();

    [Fact]
    public async Task The_model_proposes_and_the_validator_discards_what_it_cannot_back_up()
    {
        _gemini.ExtractAnswer = """
            { "requirements": [
                { "text": "Kubernetes in production", "importance": "must", "line": 2 },
                { "text": "Team leadership", "importance": "nice", "line": 3 },
                { "text": "Docker", "importance": "must", "line": 4 }
            ] }
            """;
        // The model claims Kubernetes on the strength of a line that does not exist, and
        // leadership on a GitHub fact that was never gathered. Only Docker is real.
        _gemini.MatchAnswer = """
            { "matches": [
                { "requirement": "R1", "status": "confirmed", "reason": "Kubernetes experience on line 7.", "resumeLines": [7], "gitHubFacts": [] },
                { "requirement": "R2", "status": "partial", "reason": "Leads projects on GitHub.", "resumeLines": [], "gitHubFacts": ["G9"] },
                { "requirement": "R3", "status": "confirmed", "reason": "Containerised services with Docker.", "resumeLines": [2], "gitHubFacts": [] }
            ] }
            """;

        var analysis = await WithModel().AnalyseAsync(Resume, Vacancy);

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal("gemini-test", analysis.Engine);

        var result = analysis.Result!;
        Assert.Null(result.EngineNote);
        Assert.Equal(
            [MatchStatus.Missing, MatchStatus.Missing, MatchStatus.Confirmed],
            result.Requirements.Select(r => r.Status));
        Assert.Equal("No line in the resume or fact from GitHub supports this.", result.Requirements[0].Reason);
        Assert.Equal("Containerised services with Docker.", result.Requirements[2].Reason);
        Assert.Equal([2], result.Requirements[2].ResumeLines);

        // Two must-haves and a nice-to-have, with one must-have proven: 2 of 5.
        Assert.Equal(40, result.Score);

        Assert.Equal(
            new LlmUsage("gemini-test", 2, 2 * StubGemini.InputTokens, 2 * StubGemini.OutputTokens, result.Usage!.DurationMs),
            result.Usage);
    }

    [Fact]
    public async Task The_keyword_engine_steps_in_when_the_model_is_unavailable()
    {
        _gemini.Refuse = true;

        var analysis = await WithModel().AnalyseAsync(Resume, Vacancy);

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal("keyword", analysis.Engine);

        var result = analysis.Result!;
        Assert.Equal("The language model was unavailable, so this report was produced by keyword matching.", result.EngineNote);
        Assert.Null(result.Usage);
        Assert.Equal(
            [MatchStatus.Missing, MatchStatus.Missing, MatchStatus.Confirmed],
            result.Requirements.Select(r => r.Status));
    }

    [Fact]
    public async Task The_keyword_engine_finishes_the_job_when_the_model_fails_halfway()
    {
        _gemini.ExtractAnswer = """{ "requirements": [{ "text": "Docker", "importance": "must", "line": 4 }] }""";
        _gemini.MatchAnswer = "I could not decide.";

        var analysis = await WithModel().AnalyseAsync(Resume, Vacancy);

        Assert.Equal("keyword", analysis.Engine);
        var result = analysis.Result!;
        Assert.NotNull(result.EngineNote);
        // The requirement the model extracted is kept and matched by keyword.
        var docker = Assert.Single(result.Requirements);
        Assert.Equal(("Docker", MatchStatus.Confirmed), (docker.Text, docker.Status));
    }

    private HttpClient WithModel() => factory
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Llm:ApiKey", "test-key");
            builder.UseSetting("Llm:Model", "gemini-test");
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => _gemini));
        })
        .CreateClient();
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses;

namespace ProofFit.Api.Tests;

[Collection(ApiCollection.Name)]
public class RerunTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Docker
        - Kubernetes in production
        - PostgreSQL and Redis
        """;

    private static readonly string[] Resume = ["Backend developer", "Containerised services with Docker", "Ran PostgreSQL in production"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Comparing_again_judges_the_requirements_as_the_user_corrected_them()
    {
        var original = await _client.AnalyseAsync(Resume, Vacancy);

        // The user drops Kubernetes, splits "PostgreSQL and Redis", and marks Redis optional.
        var response = await _client.PostAsJsonAsync($"/api/analyses/{original.Id}/rerun", new
        {
            requirements = new object[]
            {
                new { text = "Docker", importance = "mustHave", vacancyLine = 2 },
                new { text = "PostgreSQL", importance = "mustHave", vacancyLine = 4 },
                new { text = "Redis", importance = "niceToHave", vacancyLine = 4 },
            },
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.ReadAsync<AnalysisResponse>();
        Assert.True(started.RequirementsEdited);
        Assert.NotEqual(original.Id, started.Id);

        var rerun = await _client.WaitForAnalysisAsync(started.Id);
        Assert.Equal(AnalysisStatus.Completed, rerun.Status);
        Assert.Equal(original.Resume.Id, rerun.Resume.Id);
        Assert.Equal(original.Vacancy.Id, rerun.Vacancy.Id);

        var requirements = rerun.Result!.Requirements;
        Assert.Equal(
            [("Docker", Importance.MustHave, MatchStatus.Confirmed, (int?)2),
             ("PostgreSQL", Importance.MustHave, MatchStatus.Confirmed, 4),
             ("Redis", Importance.NiceToHave, MatchStatus.Missing, 4)],
            requirements.Select(r => (r.Text, r.Importance, r.Status, r.VacancyLine)));

        // The original comparison is left as it was.
        var unchanged = await _client.GetFromJsonAsync<AnalysisResponse>($"/api/analyses/{original.Id}", ApiJson.Options);
        Assert.False(unchanged!.RequirementsEdited);
        Assert.Equal(3, unchanged.Result!.Requirements.Count);
    }

    [Fact]
    public async Task A_line_number_outside_the_vacancy_is_dropped_rather_than_kept()
    {
        var original = await _client.AnalyseAsync(Resume, Vacancy);

        var response = await _client.PostAsJsonAsync($"/api/analyses/{original.Id}/rerun", new
        {
            requirements = new[] { new { text = "Docker", importance = "mustHave", vacancyLine = 99 } },
        });
        var rerun = await _client.WaitForAnalysisAsync((await response.ReadAsync<AnalysisResponse>()).Id);

        Assert.Null(Assert.Single(rerun.Result!.Requirements).VacancyLine);
    }

    [Theory]
    [InlineData(0, "", "Keep at least one requirement.")]
    [InlineData(1, "   ", "A requirement cannot be empty.")]
    [InlineData(41, "Docker", "Keep at most 40 requirements.")]
    public async Task Rejects_a_list_that_cannot_be_compared(int count, string text, string message)
    {
        var original = await _client.AnalyseAsync(Resume, Vacancy);

        var response = await _client.PostAsJsonAsync($"/api/analyses/{original.Id}/rerun", new
        {
            requirements = Enumerable.Repeat(new { text, importance = "mustHave" }, count),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal([message], problem.Errors["requirements"]);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_comparison()
    {
        var response = await _client.PostAsJsonAsync($"/api/analyses/{Guid.NewGuid()}/rerun", new
        {
            requirements = new[] { new { text = "Docker", importance = "mustHave" } },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofFit.Api.Data;
using ProofFit.Api.Features.Analyses;
using ProofFit.Api.Features.Resumes;

namespace ProofFit.Api.Tests;

[Collection(ApiCollection.Name)]
public class RetentionTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Docker
        - Kubernetes in production
        - PostgreSQL and Redis
        """;

    private static readonly string[] Resume = ["Backend developer", "Containerised services with Docker"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_comparison_says_when_it_will_be_deleted()
    {
        var analysis = await _client.AnalyseAsync(Resume, Vacancy);

        Assert.Equal(analysis.CreatedAt + TimeSpan.FromHours(24), analysis.ExpiresAt);
    }

    [Fact]
    public async Task Deleting_a_comparison_removes_it_and_its_documents()
    {
        var analysis = await _client.AnalyseAsync(Resume, Vacancy);

        var response = await _client.DeleteAsync($"/api/analyses/{analysis.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/analyses/{analysis.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/resumes/{analysis.Resume.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/vacancies/{analysis.Vacancy.Id}"));
    }

    [Fact]
    public async Task Deleting_a_comparison_keeps_a_resume_another_comparison_still_uses()
    {
        var resume = await _client.UploadResumeAsync(Resume);
        var first = await RunAsync(resume);
        var second = await RunAsync(resume);

        await _client.DeleteAsync($"/api/analyses/{first.Id}");

        Assert.Equal(HttpStatusCode.OK, await StatusOf($"/api/resumes/{resume.Id}"));
        Assert.Equal(HttpStatusCode.OK, await StatusOf($"/api/analyses/{second.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/vacancies/{first.Vacancy.Id}"));
    }

    [Fact]
    public async Task Deleting_an_unknown_comparison_returns_404()
    {
        var response = await _client.DeleteAsync($"/api/analyses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_cleanup_deletes_what_is_older_than_the_cutoff_and_nothing_newer()
    {
        var old = await _client.AnalyseAsync(Resume, Vacancy);
        var recent = await _client.AnalyseAsync(Resume, Vacancy);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2);

        // Age the first comparison and its documents as if they were made two days ago.
        await db.Analyses.Where(a => a.Id == old.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, twoDaysAgo));
        await db.Resumes.Where(r => r.Id == old.Resume.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.UploadedAt, twoDaysAgo));
        await db.Vacancies.Where(v => v.Id == old.Vacancy.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.CreatedAt, twoDaysAgo));

        var removed = await scope.ServiceProvider.GetRequiredService<RetentionCleaner>()
            .DeleteExpiredAsync(DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/analyses/{old.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/resumes/{old.Resume.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf($"/api/vacancies/{old.Vacancy.Id}"));
        Assert.Equal(HttpStatusCode.OK, await StatusOf($"/api/analyses/{recent.Id}"));
        Assert.Equal(HttpStatusCode.OK, await StatusOf($"/api/resumes/{recent.Resume.Id}"));
    }

    private async Task<AnalysisResponse> RunAsync(ResumeResponse resume)
    {
        var vacancy = await _client.CreateVacancyAsync(Vacancy);
        var started = await _client.StartAnalysisAsync(resume.Id, vacancy.Id);
        return await _client.WaitForAnalysisAsync(started.Id);
    }

    private async Task<HttpStatusCode> StatusOf(string path) => (await _client.GetAsync(path)).StatusCode;
}

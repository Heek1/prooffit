using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses;

namespace ProofFit.Api.Tests;

[Collection(ApiCollection.Name)]
public class AnalysisEndpointsTests(ApiFactory factory)
{
    private const string Vacancy = """
        We are hiring a full-stack developer.

        Requirements:
        - 2+ years with ASP.NET Core and EF Core
        - React with TypeScript
        - Docker and Kubernetes

        Nice to have:
        - Angular
        """;

    private static readonly string[] Resume =
    [
        "Vlad, full-stack developer",
        "Built a billing API with ASP.NET Core and Entity Framework Core",
        "React and TypeScript frontends",
        "PostgreSQL, Docker",
    ];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task An_analysis_runs_in_the_background_and_ends_with_a_report()
    {
        var resume = await _client.UploadResumeAsync(Resume);
        var vacancy = await _client.CreateVacancyAsync(Vacancy);

        var response = await _client.PostAsJsonAsync("/api/analyses", new { resumeId = resume.Id, vacancyId = vacancy.Id });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.ReadAsync<AnalysisResponse>();
        Assert.Equal($"/api/analyses/{started.Id}", response.Headers.Location?.AbsolutePath);

        var analysis = await _client.WaitForAnalysisAsync(started.Id);

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal(AnalysisStage.Done, analysis.Stage);
        Assert.Equal("keyword", analysis.Engine);
        Assert.Null(analysis.Error);
        Assert.NotNull(analysis.CompletedAt);
        Assert.Equal(resume.Text, analysis.Resume.Text);
        Assert.Equal(vacancy.Text, analysis.Vacancy.Text);

        var result = analysis.Result;
        Assert.NotNull(result);
        Assert.Equal(
        [
            ("R1", MatchStatus.Confirmed, Importance.MustHave),
            ("R2", MatchStatus.Confirmed, Importance.MustHave),
            ("R3", MatchStatus.Partial, Importance.MustHave),
            ("R4", MatchStatus.Missing, Importance.NiceToHave),
        ], result.Requirements.Select(r => (r.Id, r.Status, r.Importance)));

        // The evidence points at real lines: line 2 of the resume, line 4 of the vacancy.
        var first = result.Requirements[0];
        Assert.Equal("2+ years with ASP.NET Core and EF Core", first.Text);
        Assert.Equal(4, first.VacancyLine);
        Assert.Equal([2], first.ResumeLines);

        // Must-haves weigh 2 and the nice-to-have 1: (2 + 2 + 1 + 0) of 7.
        Assert.Equal(71, result.Score);
    }

    [Fact]
    public async Task The_event_stream_reports_progress_and_closes_when_the_analysis_ends()
    {
        var resume = await _client.UploadResumeAsync(Resume);
        var vacancy = await _client.CreateVacancyAsync(Vacancy);
        var started = await _client.StartAnalysisAsync(resume.Id, vacancy.Id);

        // Reading to the end only returns because the server closes the stream.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stream = await _client.GetStringAsync($"/api/analyses/{started.Id}/events", timeout.Token);

        var events = stream
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("data: "))
            .Select(line => JsonSerializer.Deserialize<AnalysisProgress>(line["data: ".Length..], ApiJson.Options)!)
            .ToList();

        Assert.Contains("event: progress", stream);
        Assert.NotEmpty(events);
        Assert.Equal(new AnalysisProgress(AnalysisStatus.Completed, AnalysisStage.Done), events[^1]);
        Assert.Equal(events.Distinct(), events);
    }

    [Fact]
    public async Task An_analysis_fails_with_a_reason_when_the_vacancy_asks_for_nothing_recognisable()
    {
        var resume = await _client.UploadResumeAsync(Resume);
        var vacancy = await _client.CreateVacancyAsync("We are a friendly company looking for a great person to join our team.");
        var started = await _client.StartAnalysisAsync(resume.Id, vacancy.Id);

        var analysis = await _client.WaitForAnalysisAsync(started.Id);

        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Null(analysis.Result);
        Assert.StartsWith("No requirements were found in the vacancy.", analysis.Error);
    }

    [Fact]
    public async Task Create_rejects_ids_that_do_not_exist()
    {
        var resume = await _client.UploadResumeAsync(Resume);

        var response = await _client.PostAsJsonAsync("/api/analyses",
            new { resumeId = resume.Id, vacancyId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["vacancyId"], problem?.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/events")]
    public async Task Unknown_analysis_ids_return_404(string suffix)
    {
        var response = await _client.GetAsync($"/api/analyses/{Guid.NewGuid()}{suffix}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

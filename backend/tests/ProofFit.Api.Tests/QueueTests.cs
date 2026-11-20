using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses;

namespace ProofFit.Api.Tests;

/// <summary>The analyses table is the queue, shared by every instance of the API.</summary>
[Collection(ApiCollection.Name)]
public class QueueTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Docker in production
        - PostgreSQL and Redis
        """;

    private static readonly string[] Resume = ["Backend developer", "Containerised services with Docker"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Two_instances_on_one_database_run_each_analysis_once()
    {
        await using var second = factory.WithWebHostBuilder(_ => { });
        var clients = new[] { _client, second.CreateClient() };

        var started = new List<Guid>();
        for (var index = 0; index < 8; index++)
        {
            var client = clients[index % 2];
            var resume = await client.UploadResumeAsync(Resume);
            var vacancy = await client.CreateVacancyAsync(Vacancy);
            started.Add((await client.StartAnalysisAsync(resume.Id, vacancy.Id)).Id);
        }
        foreach (var id in started) Assert.Equal(AnalysisStatus.Completed, (await _client.WaitForAnalysisAsync(id)).Status);

        await using var db = Db(out var scope);
        await using var _ = scope;
        var attempts = await db.Analyses.Where(a => started.Contains(a.Id)).Select(a => a.Attempts).ToListAsync();
        Assert.All(attempts, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task Workers_claiming_at_the_same_moment_never_take_the_same_analysis()
    {
        var ids = new List<Guid>();
        for (var index = 0; index < 10; index++) ids.Add(await InsertAsync(AnalysisStatus.Queued, lease: null, attempts: 0));

        var claims = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AnalysisLeases>().ClaimAsync(CancellationToken.None);
        }));

        var claimed = claims.OfType<ClaimedAnalysis>().Select(claim => claim.Id).ToList();
        Assert.NotEmpty(claimed);
        Assert.Equal(claimed.Count, claimed.Distinct().Count());

        // Hand them back so the worker finishes them and nothing is left over for other tests.
        await ReleaseAsync(claimed);
        foreach (var id in ids) await _client.WaitForAnalysisAsync(id);
    }

    [Fact]
    public async Task An_analysis_whose_worker_stopped_is_taken_over()
    {
        var id = await InsertAsync(AnalysisStatus.Running, lease: DateTimeOffset.UtcNow.AddMinutes(-1), attempts: 1);
        Wake();

        var analysis = await _client.WaitForAnalysisAsync(id);

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal(2, await AttemptsAsync(id));
    }

    [Fact]
    public async Task An_analysis_another_worker_still_holds_is_left_alone()
    {
        var id = await InsertAsync(AnalysisStatus.Running, lease: DateTimeOffset.UtcNow.AddMinutes(10), attempts: 1);
        Wake();
        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Equal(1, await AttemptsAsync(id));

        await ReleaseAsync([id]);
        Assert.Equal(AnalysisStatus.Completed, (await _client.WaitForAnalysisAsync(id)).Status);
    }

    [Fact]
    public async Task An_analysis_abandoned_too_often_is_failed_instead_of_tried_again()
    {
        var id = await InsertAsync(AnalysisStatus.Running, lease: DateTimeOffset.UtcNow.AddMinutes(-1), attempts: AnalysisLeases.MaxAttempts);
        Wake();

        var analysis = await _client.WaitForAnalysisAsync(id);

        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Equal("The analysis could not be finished. Try again.", analysis.Error);
    }

    private async Task<Guid> InsertAsync(AnalysisStatus status, DateTimeOffset? lease, int attempts)
    {
        var resume = await _client.UploadResumeAsync(Resume);
        var vacancy = await _client.CreateVacancyAsync(Vacancy);

        await using var db = Db(out var scope);
        await using var _ = scope;
        var analysis = new Analysis
        {
            Id = Guid.CreateVersion7(),
            ResumeId = resume.Id,
            VacancyId = vacancy.Id,
            Status = status,
            Stage = AnalysisStage.Queued,
            LeaseExpiresAt = lease,
            Attempts = attempts,
            CreatedAt = Timestamp.Now(),
        };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync();
        return analysis.Id;
    }

    private async Task ReleaseAsync(IReadOnlyList<Guid> ids)
    {
        await using var db = Db(out var scope);
        await using var _ = scope;
        await db.Analyses.Where(a => ids.Contains(a.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.LeaseExpiresAt, (DateTimeOffset?)null));
        Wake();
    }

    private async Task<int> AttemptsAsync(Guid id)
    {
        await using var db = Db(out var scope);
        await using var _ = scope;
        return await db.Analyses.Where(a => a.Id == id).Select(a => a.Attempts).SingleAsync();
    }

    private void Wake() => factory.Services.GetRequiredService<AnalysisQueue>().Wake();

    private AppDbContext Db(out AsyncServiceScope scope)
    {
        scope = factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

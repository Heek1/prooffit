using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses;

public class RetentionOptions
{
    /// <summary>How long a comparison and its documents are kept before they are deleted.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Removes comparisons and the documents they used. There are no accounts, so a report is
/// open to anyone who has its address; keeping personal documents only briefly is what
/// limits that exposure.
/// </summary>
public class RetentionCleaner(AppDbContext db)
{
    /// <summary>Deletes everything created before the cutoff. Returns the number of comparisons removed.</summary>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var removed = await db.Analyses.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
        await DeleteUnusedDocumentsAsync(
            db.Resumes.Where(r => r.UploadedAt < cutoff), db.Vacancies.Where(v => v.CreatedAt < cutoff), ct);
        return removed;
    }

    /// <summary>Deletes one comparison now, with its documents. False when it does not exist.</summary>
    public async Task<bool> DeleteAsync(Guid analysisId, CancellationToken ct)
    {
        var analysis = await db.Analyses.AsNoTracking().SingleOrDefaultAsync(a => a.Id == analysisId, ct);
        if (analysis is null) return false;

        await db.Analyses.Where(a => a.Id == analysisId).ExecuteDeleteAsync(ct);
        await DeleteUnusedDocumentsAsync(
            db.Resumes.Where(r => r.Id == analysis.ResumeId), db.Vacancies.Where(v => v.Id == analysis.VacancyId), ct);
        return true;
    }

    // A document can serve several comparisons, so it goes only when the last one has gone.
    private async Task DeleteUnusedDocumentsAsync(
        IQueryable<Resume> resumes, IQueryable<Vacancy> vacancies, CancellationToken ct)
    {
        await resumes.Where(r => !db.Analyses.Any(a => a.ResumeId == r.Id)).ExecuteDeleteAsync(ct);
        await vacancies.Where(v => !db.Analyses.Any(a => a.VacancyId == v.Id)).ExecuteDeleteAsync(ct);
    }
}

/// <summary>Runs the cleanup once an hour for as long as the application is up.</summary>
public sealed class RetentionWorker(
    IServiceScopeFactory scopes, IOptions<RetentionOptions> options, ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await scope.ServiceProvider.GetRequiredService<RetentionCleaner>()
                    .DeleteExpiredAsync(Timestamp.Now() - options.Value.Lifetime, stoppingToken);
                if (removed > 0) logger.LogInformation("Deleted {Count} expired comparisons", removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed pass must not end the cleanup for good; the next hour tries again.
                logger.LogWarning(ex, "Could not delete expired comparisons");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

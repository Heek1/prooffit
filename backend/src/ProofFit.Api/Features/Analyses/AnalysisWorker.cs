using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses;

/// <summary>
/// Wakes this process's worker when an analysis is created here, so it starts at once
/// instead of at the next poll. The analyses table is the queue; this only says "look now".
/// </summary>
public sealed class AnalysisQueue
{
    private readonly SemaphoreSlim _signal = new(0);

    public void Wake() => _signal.Release();

    /// <summary>Returns when woken or when the timeout passes, whichever comes first.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}

/// <summary>An analysis a worker has taken on, and how many times it has been taken on.</summary>
public record ClaimedAnalysis(Guid Id, int Attempts);

/// <summary>
/// Hands out analyses to workers through the database, so any number of API instances can
/// share the work. Each claim is a lease: the worker renews it while it runs, and when an
/// instance dies its lease runs out and another instance takes the analysis over.
/// </summary>
public sealed class AnalysisLeases(AppDbContext db)
{
    /// <summary>How long a claim holds before another worker may take the analysis over.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    /// <summary>An analysis taken on more often than this is failed rather than tried again.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Takes the oldest analysis that is waiting or whose worker stopped renewing it; null when there is none.</summary>
    /// <remarks>
    /// SKIP LOCKED lets several workers claim at the same moment without waiting on each
    /// other or taking the same row. The database's clock decides when a lease has run out,
    /// so instances whose clocks disagree still agree on that.
    /// </remarks>
    public async Task<ClaimedAnalysis?> ClaimAsync(CancellationToken ct)
    {
        var seconds = (int)Lease.TotalSeconds;
        var claimed = await db.Database.SqlQuery<ClaimedAnalysis>($"""
            UPDATE "Analyses"
            SET "LeaseExpiresAt" = now() + make_interval(secs => {seconds}), "Attempts" = "Attempts" + 1
            WHERE "Id" = (
                SELECT "Id" FROM "Analyses"
                WHERE "Status" IN ('Queued', 'Running')
                  AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" < now())
                ORDER BY "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING "Id", "Attempts"
            """).ToListAsync(ct);
        return claimed.SingleOrDefault();
    }

    public Task RenewAsync(Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync(
            $"""UPDATE "Analyses" SET "LeaseExpiresAt" = now() + make_interval(secs => {(int)Lease.TotalSeconds}) WHERE "Id" = {id}""", ct);

    /// <summary>Gives the analysis back at once, as on shutdown, so the next worker need not wait for the lease to run out.</summary>
    public Task ReleaseAsync(Guid id, CancellationToken ct) =>
        db.Analyses.Where(a => a.Id == id).ExecuteUpdateAsync(set => set.SetProperty(a => a.LeaseExpiresAt, (DateTimeOffset?)null), ct);

    public Task GiveUpAsync(Guid id, CancellationToken ct) =>
        db.Analyses
            .Where(a => a.Id == id && (a.Status == AnalysisStatus.Queued || a.Status == AnalysisStatus.Running))
            .ExecuteUpdateAsync(set => set
                .SetProperty(a => a.Status, AnalysisStatus.Failed)
                .SetProperty(a => a.Error, "The analysis could not be finished. Try again.")
                .SetProperty(a => a.CompletedAt, Timestamp.Now()), ct);
}

/// <summary>
/// Runs analyses outside the HTTP request, so creating one returns at once and a slow model
/// call never holds a connection open.
/// </summary>
public sealed class AnalysisWorker(
    AnalysisQueue queue, IServiceScopeFactory scopes, ILogger<AnalysisWorker> logger) : BackgroundService
{
    private const int MaxConcurrentAnalyses = 2;

    // Analyses created on another instance are found this often. Ones created here wake
    // the worker at once.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var slots = new SemaphoreSlim(MaxConcurrentAnalyses);
        try
        {
            while (true)
            {
                await slots.WaitAsync(stoppingToken);
                var claim = await TryClaimAsync(stoppingToken);
                if (claim is null)
                {
                    slots.Release();
                    await queue.WaitAsync(PollInterval, stoppingToken);
                    continue;
                }

                _ = RunAsync(claim, slots, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        // Let analyses in progress hand their leases back before the host stops.
        for (var slot = 0; slot < MaxConcurrentAnalyses; slot++) await slots.WaitAsync(CancellationToken.None);
    }

    private async Task<ClaimedAnalysis?> TryClaimAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AnalysisLeases>().ClaimAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unreachable database must not stop the host; the next poll tries again.
            logger.LogWarning(ex, "Could not look for analyses to run");
            return null;
        }
    }

    private async Task RunAsync(ClaimedAnalysis claim, SemaphoreSlim slots, CancellationToken stoppingToken)
    {
        try
        {
            if (claim.Attempts > AnalysisLeases.MaxAttempts)
            {
                // Taken on and abandoned this often, it is what stops the workers, not a
                // passing fault, so it is failed instead of tried again.
                logger.LogError("Analysis {AnalysisId} was abandoned {Attempts} times and is given up", claim.Id, claim.Attempts - 1);
                await WithLeasesAsync(leases => leases.GiveUpAsync(claim.Id, CancellationToken.None));
                return;
            }

            using var running = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var heartbeat = RenewWhileRunningAsync(claim.Id, running.Token);
            try
            {
                // Each analysis gets its own scope, and so its own DbContext.
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AnalysisRunner>().RunAsync(claim.Id, stoppingToken);
            }
            finally
            {
                await running.CancelAsync();
                await heartbeat;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await WithLeasesAsync(leases => leases.ReleaseAsync(claim.Id, CancellationToken.None));
        }
        catch (Exception ex)
        {
            // The runner handles its own failures. What reaches here is the analysis
            // vanishing mid-run, as when it is deleted; that must not stop the worker.
            logger.LogWarning(ex, "Analysis {AnalysisId} could not be finished", claim.Id);
        }
        finally
        {
            slots.Release();
        }
    }

    private async Task RenewWhileRunningAsync(Guid id, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(AnalysisLeases.Lease / 4);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await WithLeasesAsync(leases => leases.RenewAsync(id, ct));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WithLeasesAsync(Func<AnalysisLeases, Task> action)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await action(scope.ServiceProvider.GetRequiredService<AnalysisLeases>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missed renewal is retried on the next tick; a missed release only means the
            // next worker waits for the lease to run out.
            logger.LogWarning(ex, "Could not update the lease on an analysis");
        }
    }
}

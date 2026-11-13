using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses;

/// <summary>
/// Hands analysis ids from the request that creates them to the background worker. The
/// database row is the durable record; this queue only says "look at this one now".
/// </summary>
public sealed class AnalysisQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    // Writing to an unbounded channel cannot fail while it is open.
    public void Enqueue(Guid analysisId) => _channel.Writer.TryWrite(analysisId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Runs analyses outside the HTTP request, so creating one returns at once and a slow model
/// call never holds a connection open.
/// </summary>
public sealed class AnalysisWorker(
    AnalysisQueue queue, IServiceScopeFactory scopes, ILogger<AnalysisWorker> logger) : BackgroundService
{
    private const int MaxConcurrentAnalyses = 2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrentAnalyses,
            CancellationToken = stoppingToken,
        };
        await Parallel.ForEachAsync(queue.ReadAllAsync(stoppingToken), options, async (analysisId, ct) =>
        {
            // Each analysis gets its own scope, and so its own DbContext.
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<AnalysisRunner>().RunAsync(analysisId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The runner handles its own failures. What reaches here is the analysis
                // vanishing mid-run, as when it is deleted; that must not stop the worker.
                logger.LogWarning(ex, "Analysis {AnalysisId} could not be finished", analysisId);
            }
        });
    }

    // The queue lives in memory, so analyses that were waiting or mid-run when the process
    // last stopped would otherwise never finish.
    private async Task RequeueUnfinishedAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unfinished = await db.Analyses
                .Where(a => a.Status == AnalysisStatus.Queued || a.Status == AnalysisStatus.Running)
                .OrderBy(a => a.CreatedAt)
                .Select(a => a.Id)
                .ToListAsync(ct);

            foreach (var analysisId in unfinished) queue.Enqueue(analysisId);
            if (unfinished.Count > 0)
                logger.LogInformation("Requeued {Count} unfinished analyses", unfinished.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unreachable database must not stop the host; new analyses still get queued.
            logger.LogWarning(ex, "Could not look for unfinished analyses");
        }
    }
}

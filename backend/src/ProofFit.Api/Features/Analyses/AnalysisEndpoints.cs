using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Resumes;
using ProofFit.Api.Features.Vacancies;

namespace ProofFit.Api.Features.Analyses;

public record CreateAnalysisRequest(Guid ResumeId, Guid VacancyId);

/// <summary>An analysis with the two documents it compares, so a report can quote them.</summary>
public record AnalysisResponse(
    Guid Id,
    AnalysisStatus Status,
    AnalysisStage Stage,
    string? Engine,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    ResumeResponse Resume,
    VacancyResponse Vacancy,
    AnalysisResult? Result)
{
    public static AnalysisResponse From(Analysis analysis) => new(
        analysis.Id, analysis.Status, analysis.Stage, analysis.Engine, analysis.Error,
        analysis.CreatedAt, analysis.CompletedAt,
        ResumeResponse.From(analysis.Resume), VacancyResponse.From(analysis.Vacancy), analysis.Result);
}

public record AnalysisProgress(AnalysisStatus Status, AnalysisStage Stage);

public static class AnalysisEndpoints
{
    private static readonly TimeSpan ProgressPollInterval = TimeSpan.FromMilliseconds(300);

    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyses").WithTags("Analyses");

        group.MapPost("/", Create);
        group.MapGet("/{id:guid}", Get).WithName("GetAnalysis");
        group.MapGet("/{id:guid}/events", Events);

        return app;
    }

    private static async Task<Results<AcceptedAtRoute<AnalysisResponse>, ValidationProblem>> Create(
        CreateAnalysisRequest request, AppDbContext db, AnalysisQueue queue, CancellationToken ct)
    {
        var resume = await db.Resumes.FindAsync([request.ResumeId], ct);
        var vacancy = await db.Vacancies.FindAsync([request.VacancyId], ct);

        var errors = new Dictionary<string, string[]>();
        if (resume is null) errors["resumeId"] = ["There is no resume with this id."];
        if (vacancy is null) errors["vacancyId"] = ["There is no vacancy with this id."];
        if (resume is null || vacancy is null) return TypedResults.ValidationProblem(errors);

        var analysis = new Analysis
        {
            Id = Guid.CreateVersion7(),
            Resume = resume,
            Vacancy = vacancy,
            Status = AnalysisStatus.Queued,
            Stage = AnalysisStage.Queued,
            CreatedAt = Timestamp.Now(),
        };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);

        // Queued only after the row is committed. If the process dies in between, the
        // worker finds the row on its next start.
        queue.Enqueue(analysis.Id);

        return TypedResults.AcceptedAtRoute(AnalysisResponse.From(analysis), "GetAnalysis", new { id = analysis.Id });
    }

    private static async Task<Results<Ok<AnalysisResponse>, NotFound>> Get(Guid id, AppDbContext db, CancellationToken ct)
    {
        var analysis = await db.Analyses
            .AsNoTracking()
            .Include(a => a.Resume)
            .Include(a => a.Vacancy)
            .SingleOrDefaultAsync(a => a.Id == id, ct);

        return analysis is null ? TypedResults.NotFound() : TypedResults.Ok(AnalysisResponse.From(analysis));
    }

    private static async Task<Results<ServerSentEventsResult<AnalysisProgress>, NotFound>> Events(
        Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Analyses.AnyAsync(a => a.Id == id, ct)) return TypedResults.NotFound();

        return TypedResults.ServerSentEvents(WatchAsync(id, db, ct), eventType: "progress");
    }

    // Emits the current state at once, then every change, and ends with the final state.
    // Progress is read from the database rather than pushed from the worker: it costs a
    // small query a few times a second for the seconds an analysis runs, and in exchange
    // it works unchanged when the API and the worker are separate processes.
    private static async IAsyncEnumerable<AnalysisProgress> WatchAsync(
        Guid id, AppDbContext db, [EnumeratorCancellation] CancellationToken ct)
    {
        AnalysisProgress? last = null;
        while (true)
        {
            var current = await db.Analyses
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new AnalysisProgress(a.Status, a.Stage))
                .SingleAsync(ct);

            if (current != last)
            {
                yield return current;
                last = current;
            }

            if (current.Status is AnalysisStatus.Completed or AnalysisStatus.Failed) yield break;
            await Task.Delay(ProgressPollInterval, ct);
        }
    }
}

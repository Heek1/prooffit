using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses;

/// <param name="Requirements">The requirements to judge, as the user corrected them.</param>
public record RerunRequest(IReadOnlyList<RerunRequirement>? Requirements);

/// <param name="VacancyLine">The vacancy line the requirement came from, kept when the user only reworded it.</param>
public record RerunRequirement(string? Text, Importance Importance, int? VacancyLine);

/// <summary>
/// Compares the same documents again against a list of requirements the user has corrected.
/// Reading a vacancy is the step most open to doubt, so the user gets the last word on what
/// it asks for, and the evidence is then checked exactly as before.
/// </summary>
public static class RerunEndpoint
{
    public const int MaxRequirements = 40;
    public const int MaxRequirementLength = 300;

    public static IEndpointRouteBuilder MapRerunEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/analyses/{id:guid}/rerun", Rerun)
            .WithTags("Analyses")
            .RequireRateLimiting(RateLimits.Analyses);
        return app;
    }

    private static async Task<Results<AcceptedAtRoute<AnalysisResponse>, NotFound, ValidationProblem>> Rerun(
        Guid id, RerunRequest request, AppDbContext db, AnalysisQueue queue,
        IOptions<RetentionOptions> retention, CancellationToken ct)
    {
        var source = await db.Analyses
            .Include(a => a.Resume)
            .Include(a => a.Vacancy)
            .SingleOrDefaultAsync(a => a.Id == id, ct);
        if (source is null) return TypedResults.NotFound();

        var given = (request.Requirements ?? [])
            .Select(r => new GivenRequirement((r.Text ?? "").Trim(), r.Importance, r.VacancyLine))
            .ToList();

        var error = given switch
        {
            [] => "Keep at least one requirement.",
            { Count: > MaxRequirements } => $"Keep at most {MaxRequirements} requirements.",
            _ when given.Any(r => r.Text.Length == 0) => "A requirement cannot be empty.",
            _ when given.Any(r => r.Text.Length > MaxRequirementLength) =>
                $"A requirement can be at most {MaxRequirementLength} characters.",
            _ => null,
        };
        if (error is not null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["requirements"] = [error] });

        var vacancy = new NumberedText(source.Vacancy.Text);
        var analysis = new Analysis
        {
            Id = Guid.CreateVersion7(),
            Resume = source.Resume,
            Vacancy = source.Vacancy,
            GitHubLogin = source.GitHubLogin,
            // A line number is kept only while it still points into the vacancy.
            GivenRequirements = given
                .Select(r => r with { VacancyLine = r.VacancyLine is { } line && vacancy.HasContent(line) ? line : null })
                .ToList(),
            Status = AnalysisStatus.Queued,
            Stage = AnalysisStage.Queued,
            CreatedAt = Timestamp.Now(),
        };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);
        queue.Enqueue(analysis.Id);

        return TypedResults.AcceptedAtRoute(
            AnalysisResponse.From(analysis, retention.Value.Lifetime), "GetAnalysis", new { id = analysis.Id });
    }
}

using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Features.Analyses;

/// <summary>Thrown for a failure the user can act on; its message is shown to them as is.</summary>
public class AnalysisFailedException(string message) : Exception(message);

/// <summary>Carries one analysis from queued to completed or failed.</summary>
public class AnalysisRunner(
    AppDbContext db,
    IRequirementExtractor extractor,
    IRequirementMatcher matcher,
    ILogger<AnalysisRunner> logger)
{
    public async Task RunAsync(Guid analysisId, CancellationToken ct)
    {
        var analysis = await db.Analyses
            .Include(a => a.Resume)
            .Include(a => a.Vacancy)
            .SingleOrDefaultAsync(a => a.Id == analysisId, ct);

        // Finished analyses can be queued again after a restart; they are left alone.
        if (analysis is null || analysis.Status is AnalysisStatus.Completed or AnalysisStatus.Failed) return;

        try
        {
            analysis.Status = AnalysisStatus.Running;
            analysis.Result = await AnalyseAsync(analysis, ct);
            analysis.Engine = matcher.EngineName;
            analysis.Status = AnalysisStatus.Completed;
            analysis.Stage = AnalysisStage.Done;
        }
        catch (AnalysisFailedException ex)
        {
            Fail(analysis, ex.Message);
        }
        // Shutdown leaves the analysis as running, and the worker picks it up on next start.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Analysis {AnalysisId} failed", analysisId);
            Fail(analysis, "The analysis failed unexpectedly. Try again.");
        }

        analysis.CompletedAt = Timestamp.Now();
        await db.SaveChangesAsync(ct);
    }

    private async Task<AnalysisResult> AnalyseAsync(Analysis analysis, CancellationToken ct)
    {
        await EnterStageAsync(analysis, AnalysisStage.ReadingVacancy, ct);
        var extracted = await extractor.ExtractAsync(new NumberedText(analysis.Vacancy.Text), ct);
        if (extracted.Count == 0)
        {
            throw new AnalysisFailedException(
                "No requirements were found in the vacancy. Paste the full posting, including its list of requirements.");
        }

        var requirements = extracted
            .Select((r, index) => new Requirement($"R{index + 1}", r.Text, r.Importance, r.VacancyLine))
            .ToList();

        await EnterStageAsync(analysis, AnalysisStage.Matching, ct);
        var resume = new NumberedText(analysis.Resume.Text);
        var proposed = await matcher.MatchAsync(requirements, resume, ct);
        var matches = EvidenceValidator.Validate(requirements, proposed, resume);

        return new AnalysisResult(Scoring.Score(matches), matches);
    }

    // Saved at once so anyone watching the analysis sees the step it is on.
    private async Task EnterStageAsync(Analysis analysis, AnalysisStage stage, CancellationToken ct)
    {
        analysis.Stage = stage;
        await db.SaveChangesAsync(ct);
    }

    private static void Fail(Analysis analysis, string error)
    {
        analysis.Status = AnalysisStatus.Failed;
        analysis.Error = error;
        analysis.Result = null;
    }
}

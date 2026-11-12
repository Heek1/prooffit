using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;
using ProofFit.Api.Features.GitHub;
using ProofFit.Api.Features.Llm;

namespace ProofFit.Api.Features.Analyses;

/// <summary>Thrown for a failure the user can act on; its message is shown to them as is.</summary>
public class AnalysisFailedException(string message) : Exception(message);

/// <summary>Carries one analysis from queued to completed or failed.</summary>
/// <param name="extractor">The configured extractor: model-based when a model is set up, keyword otherwise.</param>
/// <param name="matcher">The configured matcher, chosen the same way.</param>
/// <param name="keywordExtractor">Steps in when the model cannot be used.</param>
/// <param name="keywordMatcher">Steps in when the model cannot be used.</param>
public class AnalysisRunner(
    AppDbContext db,
    IRequirementExtractor extractor,
    IRequirementMatcher matcher,
    KeywordRequirementExtractor keywordExtractor,
    KeywordRequirementMatcher keywordMatcher,
    IGitHubEvidenceSource gitHub,
    LlmUsageRecorder llmUsage,
    ILogger<AnalysisRunner> logger)
{
    private const string FallbackNote =
        "The language model was unavailable, so this report was produced by keyword matching.";

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
            (analysis.Result, analysis.Engine) = await AnalyseAsync(analysis, ct);
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

    private async Task<(AnalysisResult Result, string Engine)> AnalyseAsync(Analysis analysis, CancellationToken ct)
    {
        // A model that is down must not cost the user their report. From the first failed
        // call on, the keyword engine does the rest and the report says so.
        var activeMatcher = matcher;
        string? engineNote = null;

        await EnterStageAsync(analysis, AnalysisStage.ReadingVacancy, ct);
        var vacancy = new NumberedText(analysis.Vacancy.Text);
        IReadOnlyList<ExtractedRequirement> extracted;
        try
        {
            extracted = await extractor.ExtractAsync(vacancy, ct);
        }
        catch (LlmUnavailableException ex)
        {
            logger.LogWarning(ex, "Model unavailable while reading the vacancy of analysis {AnalysisId}", analysis.Id);
            (activeMatcher, engineNote) = (keywordMatcher, FallbackNote);
            extracted = await keywordExtractor.ExtractAsync(vacancy, ct);
        }

        if (extracted.Count == 0)
        {
            throw new AnalysisFailedException(
                "No requirements were found in the vacancy. Paste the full posting, including its list of requirements.");
        }

        var requirements = extracted
            .Select((r, index) => new Requirement($"R{index + 1}", r.Text, r.Importance, r.VacancyLine))
            .ToList();

        var (evidence, gitHubNote) = await CollectGitHubAsync(analysis, ct);
        var facts = evidence?.Facts ?? [];

        await EnterStageAsync(analysis, AnalysisStage.Matching, ct);
        var resume = new NumberedText(analysis.Resume.Text);
        IReadOnlyList<ProposedMatch> proposed;
        try
        {
            proposed = await activeMatcher.MatchAsync(requirements, resume, facts, ct);
        }
        catch (LlmUnavailableException ex)
        {
            logger.LogWarning(ex, "Model unavailable while matching analysis {AnalysisId}", analysis.Id);
            (activeMatcher, engineNote) = (keywordMatcher, FallbackNote);
            proposed = await keywordMatcher.MatchAsync(requirements, resume, facts, ct);
        }

        var matches = EvidenceValidator.Validate(requirements, proposed, resume, facts);
        var result = new AnalysisResult(
            Scoring.Score(matches), matches, evidence, gitHubNote, engineNote, llmUsage.Summarise());
        return (result, activeMatcher.EngineName);
    }

    // GitHub is extra evidence, never a precondition: when it cannot be read, the analysis
    // goes ahead on the resume alone and the report says why GitHub is absent.
    private async Task<(GitHubEvidence? Evidence, string? Note)> CollectGitHubAsync(Analysis analysis, CancellationToken ct)
    {
        if (analysis.GitHubLogin is not { } login) return (null, null);

        await EnterStageAsync(analysis, AnalysisStage.CollectingGitHub, ct);
        try
        {
            return (await gitHub.GetAsync(login, ct), null);
        }
        catch (GitHubUnavailableException ex)
        {
            logger.LogWarning(ex, "GitHub evidence for {Login} is unavailable", login);
            return (null, ex.Message);
        }
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

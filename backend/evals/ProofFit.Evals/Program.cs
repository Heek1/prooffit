// Measures a matcher against hand-labelled cases.
//
//   dotnet run --project evals/ProofFit.Evals                          keyword engine
//   dotnet run --project evals/ProofFit.Evals -- --engine model        model engine (needs LLM_API_KEY)
//   dotnet run --project evals/ProofFit.Evals -- --min-accuracy 0.6    fail when accuracy drops below 60%
//   dotnet run --project evals/ProofFit.Evals -- --from 10             start at the case file named 10-...
//   dotnet run --project evals/ProofFit.Evals -- --show-misses         list every wrong verdict with its reason
//   dotnet run --project evals/ProofFit.Evals -- --suite extraction    measure reading requirements from vacancies
//   ... --suite extraction --min-recall 0.7 --min-precision 0.8        fail below these
//
// A model on a free tier allows only a few calls a day, one per case here. When it stops
// answering, the run ends with the cases it finished and says where to resume.
//
// Each case gives a resume, optional GitHub facts, and requirements labelled with the
// verdict a careful human reviewer would reach. Requirements are given rather than
// extracted, so the numbers measure matching alone.

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProofFit.Api;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;
using ProofFit.Api.Features.Llm;

// Numbers print the same on every machine, whatever its regional settings.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var engine = Option("--engine") ?? "keyword";
var minAccuracy = double.Parse(Option("--min-accuracy") ?? "0", CultureInfo.InvariantCulture);
var from = Option("--from");
var showMisses = args.Contains("--show-misses");
var suite = Option("--suite") ?? "matching";

ILlmClient? llm = null;
IRequirementMatcher matcher;
switch (engine)
{
    case "keyword":
        matcher = new KeywordRequirementMatcher();
        break;
    case "model":
        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Set LLM_API_KEY to evaluate the model engine.");
            return 2;
        }
        var options = new LlmOptions { ApiKey = apiKey };
        if (Environment.GetEnvironmentVariable("LLM_MODEL") is { Length: > 0 } model) options.Model = model;
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        llm = new GeminiClient(http, Options.Create(options), new LlmUsageRecorder(), NullLogger<GeminiClient>.Instance);
        matcher = new LlmRequirementMatcher(llm);
        break;
    default:
        Console.Error.WriteLine($"Unknown engine '{engine}'. Use keyword or model.");
        return 2;
}

if (suite == "extraction") return await EvaluateExtractionAsync();
if (suite != "matching")
{
    Console.Error.WriteLine($"Unknown suite '{suite}'. Use matching or extraction.");
    return 2;
}

var cases = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "cases"), "*.json")
    .Order(StringComparer.Ordinal)
    .Where(path => from is null || string.CompareOrdinal(Path.GetFileName(path), from) >= 0)
    .Select(path => (File: Path.GetFileNameWithoutExtension(path), Case: JsonSerializer.Deserialize<EvalCase>(File.ReadAllText(path), ApiJson.Options)!))
    .ToList();
string? stoppedAt = null;

// confusion[expected][predicted]
var statuses = new[] { MatchStatus.Confirmed, MatchStatus.Partial, MatchStatus.Missing };
var confusion = statuses.ToDictionary(expected => expected, _ => statuses.ToDictionary(predicted => predicted, _ => 0));

Console.WriteLine($"Engine: {matcher.EngineName}");
Console.WriteLine();
Console.WriteLine($"{"Case",-30} Correct");

foreach (var (caseFile, evalCase) in cases)
{
    var requirements = evalCase.Requirements
        .Select((r, index) => new Requirement($"R{index + 1}", r.Text, r.Importance, null))
        .ToList();
    var resume = new NumberedText(string.Join('\n', evalCase.Resume));
    var facts = evalCase.GitHubFacts ?? [];

    // The same path an analysis takes: the matcher proposes, the validator decides.
    IReadOnlyList<ProposedMatch> proposed;
    try
    {
        proposed = await WithRetriesAsync(() => matcher.MatchAsync(requirements, resume, facts, CancellationToken.None));
    }
    catch (LlmUnavailableException ex)
    {
        stoppedAt = caseFile;
        Console.WriteLine($"{evalCase.Name,-30} not run: {ex.Message}");
        break;
    }
    var verdicts = EvidenceValidator.Validate(requirements, proposed, resume, facts);

    var correct = 0;
    var misses = new List<string>();
    for (var index = 0; index < verdicts.Count; index++)
    {
        var expected = evalCase.Requirements[index].Expected;
        confusion[expected][verdicts[index].Status]++;
        if (verdicts[index].Status == expected) correct++;
        else misses.Add($"    {evalCase.Requirements[index].Text}: expected {expected}, got {verdicts[index].Status}. {verdicts[index].Reason}");
    }
    Console.WriteLine($"{evalCase.Name,-30} {correct}/{verdicts.Count}");
    if (showMisses) misses.ForEach(Console.WriteLine);
}

var total = confusion.Values.Sum(row => row.Values.Sum());
var right = statuses.Sum(status => confusion[status][status]);
var accuracy = total == 0 ? 0 : (double)right / total;

// The costliest mistake for this product: telling someone a requirement is met when
// nothing shows it.
var unsupported = confusion[MatchStatus.Missing][MatchStatus.Confirmed] + confusion[MatchStatus.Missing][MatchStatus.Partial];

Console.WriteLine();
Console.WriteLine($"Accuracy: {right}/{total} ({accuracy:P1})");
Console.WriteLine($"Unsupported claims: {unsupported} (verdict proven or partly proven, label missing)");
Console.WriteLine();
Console.WriteLine($"{"expected \\ predicted",-22}{"confirmed",10}{"partial",10}{"missing",10}");
foreach (var expected in statuses)
{
    Console.WriteLine($"{expected.ToString().ToLowerInvariant(),-22}" +
        string.Concat(statuses.Select(predicted => $"{confusion[expected][predicted],10}")));
}

if (stoppedAt is not null)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Stopped early. Resume with: --from {stoppedAt.Split('-')[0]}");
    return 3;
}

if (accuracy < minAccuracy)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Accuracy {accuracy:P1} is below the required {minAccuracy:P1}.");
    return 1;
}
return 0;

async Task<int> EvaluateExtractionAsync()
{
    IRequirementExtractor extractor = llm is null ? new KeywordRequirementExtractor() : new LlmRequirementExtractor(llm);
    Console.WriteLine($"Extractor: {(llm is null ? "keyword" : "model")}");
    Console.WriteLine();

    var (result, stopped) = await ExtractionEval.RunAsync(extractor, from, WithRetriesAsync<IReadOnlyList<ExtractedRequirement>>);
    var recall = result.Labelled == 0 ? 0 : (double)result.Found / result.Labelled;
    var precision = result.Extracted == 0 ? 0 : (double)result.Real / result.Extracted;
    var importance = result.Found == 0 ? 0 : (double)result.ImportanceRight / result.Found;

    Console.WriteLine();
    Console.WriteLine($"Recall:     {result.Found}/{result.Labelled} ({recall:P1}) of the labelled requirements found");
    Console.WriteLine($"Precision:  {result.Real}/{result.Extracted} ({precision:P1}) of the extracted requirements are real");
    Console.WriteLine($"Importance: {result.ImportanceRight}/{result.Found} ({importance:P1}) of the found ones marked required or optional correctly");

    if (stopped is not null)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Stopped early. Resume with: --suite extraction --from {stopped.Split('-')[0]}");
        return 3;
    }

    var minRecall = double.Parse(Option("--min-recall") ?? "0", CultureInfo.InvariantCulture);
    var minPrecision = double.Parse(Option("--min-precision") ?? "0", CultureInfo.InvariantCulture);
    if (recall < minRecall || precision < minPrecision)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Below the required recall {minRecall:P1} or precision {minPrecision:P1}.");
        return 1;
    }
    return 0;
}

// A hosted model is sometimes briefly overloaded. One refused call should not throw away
// a whole evaluation run, so each case gets a few attempts.
static async Task<T> WithRetriesAsync<T>(Func<Task<T>> call)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            return await call();
        }
        catch (LlmUnavailableException) when (attempt < 4)
        {
            await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
        }
    }
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

/// <param name="Resume">The resume, one entry per line.</param>
record EvalCase(string Name, string[] Resume, GitHubFact[]? GitHubFacts, LabelledRequirement[] Requirements);

/// <param name="Why">The reasoning behind the label, for whoever reviews or extends the set.</param>
record LabelledRequirement(string Text, Importance Importance, MatchStatus Expected, string Why);

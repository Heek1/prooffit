// Measures a matcher against hand-labelled cases.
//
//   dotnet run --project evals/ProofFit.Evals                          keyword engine
//   dotnet run --project evals/ProofFit.Evals -- --engine model        model engine (needs LLM_API_KEY)
//   dotnet run --project evals/ProofFit.Evals -- --min-accuracy 0.6    fail when accuracy drops below 60%
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
        matcher = new LlmRequirementMatcher(
            new GeminiClient(http, Options.Create(options), new LlmUsageRecorder(), NullLogger<GeminiClient>.Instance));
        break;
    default:
        Console.Error.WriteLine($"Unknown engine '{engine}'. Use keyword or model.");
        return 2;
}

var cases = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "cases"), "*.json")
    .Order(StringComparer.Ordinal)
    .Select(path => JsonSerializer.Deserialize<EvalCase>(File.ReadAllText(path), ApiJson.Options)!)
    .ToList();

// confusion[expected][predicted]
var statuses = new[] { MatchStatus.Confirmed, MatchStatus.Partial, MatchStatus.Missing };
var confusion = statuses.ToDictionary(expected => expected, _ => statuses.ToDictionary(predicted => predicted, _ => 0));

Console.WriteLine($"Engine: {matcher.EngineName}");
Console.WriteLine();
Console.WriteLine($"{"Case",-30} Correct");

foreach (var evalCase in cases)
{
    var requirements = evalCase.Requirements
        .Select((r, index) => new Requirement($"R{index + 1}", r.Text, r.Importance, null))
        .ToList();
    var resume = new NumberedText(string.Join('\n', evalCase.Resume));
    var facts = evalCase.GitHubFacts ?? [];

    // The same path an analysis takes: the matcher proposes, the validator decides.
    var proposed = await matcher.MatchAsync(requirements, resume, facts, CancellationToken.None);
    var verdicts = EvidenceValidator.Validate(requirements, proposed, resume, facts);

    var correct = 0;
    for (var index = 0; index < verdicts.Count; index++)
    {
        var expected = evalCase.Requirements[index].Expected;
        confusion[expected][verdicts[index].Status]++;
        if (verdicts[index].Status == expected) correct++;
    }
    Console.WriteLine($"{evalCase.Name,-30} {correct}/{verdicts.Count}");
}

var total = confusion.Values.Sum(row => row.Values.Sum());
var right = statuses.Sum(status => confusion[status][status]);
var accuracy = (double)right / total;

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

if (accuracy < minAccuracy)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Accuracy {accuracy:P1} is below the required {minAccuracy:P1}.");
    return 1;
}
return 0;

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

/// <param name="Resume">The resume, one entry per line.</param>
record EvalCase(string Name, string[] Resume, GitHubFact[]? GitHubFacts, LabelledRequirement[] Requirements);

/// <param name="Why">The reasoning behind the label, for whoever reviews or extends the set.</param>
record LabelledRequirement(string Text, Importance Importance, MatchStatus Expected, string Why);

using System.Text.Json;
using ProofFit.Api;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// Measures how well an extractor reads requirements out of a vacancy: how many of the
/// labelled requirements it finds (recall), how many of its requirements are real ones
/// (precision), and whether it marks them required or optional correctly.
/// </summary>
/// <remarks>
/// An extracted requirement counts as finding a labelled one when its text contains one of
/// the label's phrases as a whole word. Grouping is not judged: "React with TypeScript" may
/// arrive as one requirement or two, so one extracted requirement can find several labels.
/// </remarks>
static class ExtractionEval
{
    public record Result(int Labelled, int Found, int Extracted, int Real, int ImportanceRight);

    public static async Task<(Result Result, string? StoppedAt)> RunAsync(
        IRequirementExtractor extractor, string? from, Func<Func<Task<IReadOnlyList<ExtractedRequirement>>>, Task<IReadOnlyList<ExtractedRequirement>>> withRetries)
    {
        var cases = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "extraction"), "*.json")
            .Order(StringComparer.Ordinal)
            .Where(path => from is null || string.CompareOrdinal(Path.GetFileName(path), from) >= 0)
            .Select(path => (File: Path.GetFileNameWithoutExtension(path), Case: JsonSerializer.Deserialize<ExtractionCase>(File.ReadAllText(path), ApiJson.Options)!))
            .ToList();

        Console.WriteLine($"{"Case",-34}{"Found",8}{"Extra",8}{"Importance",12}");

        int labelled = 0, found = 0, extractedTotal = 0, real = 0, importanceRight = 0;
        foreach (var (file, evalCase) in cases)
        {
            var vacancy = new NumberedText(string.Join('\n', evalCase.Vacancy));
            IReadOnlyList<ExtractedRequirement> extracted;
            try
            {
                extracted = await withRetries(() => extractor.ExtractAsync(vacancy, CancellationToken.None));
            }
            catch (ProofFit.Api.Features.Llm.LlmUnavailableException ex)
            {
                Console.WriteLine($"{evalCase.Name,-34} not run: {ex.Message}");
                return (new Result(labelled, found, extractedTotal, real, importanceRight), file);
            }

            var caseFound = 0;
            var caseImportance = 0;
            var missed = new List<string>();
            foreach (var label in evalCase.Expected)
            {
                var match = extracted.FirstOrDefault(requirement => Finds(requirement, label));
                if (match is null)
                {
                    missed.Add(label.Match[0]);
                    continue;
                }
                caseFound++;
                if (match.Importance == label.Importance) caseImportance++;
            }
            var extra = extracted.Where(requirement => !evalCase.Expected.Any(label => Finds(requirement, label))).ToList();

            labelled += evalCase.Expected.Length;
            found += caseFound;
            extractedTotal += extracted.Count;
            real += extracted.Count - extra.Count;
            importanceRight += caseImportance;

            Console.WriteLine($"{evalCase.Name,-34}{$"{caseFound}/{evalCase.Expected.Length}",8}{extra.Count,8}{$"{caseImportance}/{caseFound}",12}");
            foreach (var phrase in missed) Console.WriteLine($"    missed: {phrase}");
            foreach (var requirement in extra) Console.WriteLine($"    extra:  {requirement.Text}");
        }

        return (new Result(labelled, found, extractedTotal, real, importanceRight), null);
    }

    private static bool Finds(ExtractedRequirement requirement, ExpectedRequirement label) =>
        label.Match.Any(phrase => ContainsWord(requirement.Text, phrase));

    // A whole-word match that also works for names like "C#", "Node.js" or "Go", where
    // regular-expression word boundaries do not.
    private static bool ContainsWord(string text, string phrase)
    {
        for (var start = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase); start >= 0;
             start = text.IndexOf(phrase, start + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = start + phrase.Length;
            var before = start == 0 || !char.IsLetterOrDigit(text[start - 1]);
            var after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) return true;
        }
        return false;
    }
}

/// <param name="About">What the case tests, for whoever reviews or extends the set.</param>
/// <param name="Vacancy">The posting, one entry per line.</param>
record ExtractionCase(string Name, string About, string[] Vacancy, ExpectedRequirement[] Expected);

/// <param name="Match">Phrases any of which identify this requirement in an extracted one.</param>
record ExpectedRequirement(string[] Match, Importance Importance);

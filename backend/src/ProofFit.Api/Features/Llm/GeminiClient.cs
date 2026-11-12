using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace ProofFit.Api.Features.Llm;

/// <summary>Calls Google's Gemini API, asking for JSON that follows a schema.</summary>
public class GeminiClient(
    HttpClient http, IOptions<LlmOptions> options, LlmUsageRecorder usage, ILogger<GeminiClient> logger) : ILlmClient
{
    public string Model => options.Value.Model;

    public async Task<T> GenerateAsync<T>(LlmPrompt prompt, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl}models/{Model}:generateContent")
        {
            Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = prompt.System } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt.User } } } },
                generationConfig = new
                {
                    // The same documents should get the same verdicts.
                    temperature = 0,
                    responseMimeType = "application/json",
                    responseSchema = ToGeminiSchema(prompt.Schema),
                },
            }),
        };
        // In a header rather than the query string, so the key never lands in request logs.
        request.Headers.Add("x-goog-api-key", options.Value.ApiKey);

        var started = Stopwatch.GetTimestamp();
        GenerateResponse? response;
        try
        {
            using var reply = await http.SendAsync(request, ct);
            if (!reply.IsSuccessStatusCode)
            {
                // The body explains the refusal (bad key, quota, unknown model). It goes
                // to the log for the operator and is never shown to the user.
                logger.LogWarning("Gemini refused the {Purpose} call with {Status}: {Body}",
                    prompt.Purpose, (int)reply.StatusCode, await reply.Content.ReadAsStringAsync(ct));
                throw new LlmUnavailableException($"The model refused the request with status {(int)reply.StatusCode}.");
            }
            response = await reply.Content.ReadFromJsonAsync<GenerateResponse>(ct);
        }
        // Timeouts and network failures arrive as several exception types; unless the
        // caller cancelled, they all mean the model is out of reach.
        catch (Exception ex) when (ex is not LlmUnavailableException && !ct.IsCancellationRequested)
        {
            throw new LlmUnavailableException("The model could not be reached.", ex);
        }

        usage.Record(Model,
            response?.UsageMetadata?.PromptTokenCount ?? 0,
            response?.UsageMetadata?.CandidatesTokenCount ?? 0,
            Stopwatch.GetElapsedTime(started));

        var text = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new LlmUnavailableException("The model returned no answer.");

        try
        {
            return JsonSerializer.Deserialize<T>(text, ApiJson.Options)
                ?? throw new LlmUnavailableException("The model returned an empty answer.");
        }
        catch (JsonException ex)
        {
            throw new LlmUnavailableException("The model's answer was not the JSON that was asked for.", ex);
        }
    }

    // Gemini's schema dialect is JSON Schema with type names in capitals. Prompts are
    // written in standard JSON Schema so they stay usable with any other provider.
    private static JsonNode ToGeminiSchema(JsonNode schema)
    {
        var copy = schema.DeepClone();
        Capitalise(copy);
        return copy;

        static void Capitalise(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject fields:
                    if (fields["type"] is JsonValue type && type.TryGetValue<string>(out var name))
                        fields["type"] = name.ToUpperInvariant();
                    foreach (var (_, child) in fields.ToList()) Capitalise(child);
                    break;
                case JsonArray items:
                    foreach (var item in items) Capitalise(item);
                    break;
            }
        }
    }

    private record GenerateResponse(List<Candidate>? Candidates, Usage? UsageMetadata);

    private record Candidate(Content? Content);

    private record Content(List<Part>? Parts);

    private record Part(string? Text);

    private record Usage(int PromptTokenCount, int CandidatesTokenCount);
}

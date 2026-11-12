using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProofFit.Api.Features.Llm;

namespace ProofFit.Api.Tests.Llm;

/// <summary>
/// Stands in for the Gemini API. Each answer is the JSON the model would have written; the
/// stub wraps it in Gemini's response envelope with a fixed token count.
/// </summary>
public sealed class StubGemini : HttpMessageHandler
{
    public const int InputTokens = 100;
    public const int OutputTokens = 20;

    /// <summary>The model's answer to "what does this posting require?".</summary>
    public string ExtractAnswer { get; set; } = """{ "requirements": [] }""";

    /// <summary>The model's answer to "does the evidence meet these requirements?".</summary>
    public string MatchAnswer { get; set; } = """{ "matches": [] }""";

    /// <summary>When set, every call is refused the way Gemini refuses a bad API key.</summary>
    public bool Refuse { get; set; }

    /// <summary>When set, replaces the whole response body, envelope included.</summary>
    public string? RawBody { get; set; }

    public ConcurrentQueue<(string Url, string? ApiKey, JsonNode Body)> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
        request.Headers.TryGetValues("x-goog-api-key", out var keys);
        Requests.Enqueue((request.RequestUri!.ToString(), keys?.FirstOrDefault(), body));

        if (Refuse)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{ "error": { "message": "API key not valid." } }"""),
            };
        }

        // The two prompts are told apart by the schema they ask for.
        var isExtract = body["generationConfig"]!["responseSchema"]!["properties"]!["requirements"] is not null;
        var envelope = RawBody ?? JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = isExtract ? ExtractAnswer : MatchAnswer } } } } },
            usageMetadata = new { promptTokenCount = InputTokens, candidatesTokenCount = OutputTokens },
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) };
    }
}

/// <summary>An <see cref="ILlmClient"/> that answers with scripted JSON, for testing what is built on top of one.</summary>
public sealed class FakeLlmClient(string answer) : ILlmClient
{
    public string Model => "fake-model";

    public List<LlmPrompt> Prompts { get; } = [];

    public Task<T> GenerateAsync<T>(LlmPrompt prompt, CancellationToken ct)
    {
        Prompts.Add(prompt);
        return Task.FromResult(JsonSerializer.Deserialize<T>(answer, ApiJson.Options)!);
    }
}

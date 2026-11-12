using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Llm;

namespace ProofFit.Api.Tests.Llm;

public class GeminiClientTests
{
    private static readonly JsonNode Schema = JsonNode.Parse("""
        {
          "type": "object",
          "properties": { "requirements": { "type": "array", "items": { "type": "string" } } }
        }
        """)!;

    private readonly StubGemini _gemini = new();
    private readonly LlmUsageRecorder _usage = new();

    private record Answer(List<string> Requirements);

    [Fact]
    public async Task Returns_the_models_json_as_the_requested_type()
    {
        _gemini.ExtractAnswer = """{ "requirements": ["Docker", "Kafka"] }""";

        var answer = await Generate();

        Assert.Equal(["Docker", "Kafka"], answer.Requirements);
    }

    [Fact]
    public async Task Sends_the_prompt_and_asks_for_deterministic_json_in_the_schemas_shape()
    {
        await Generate();

        var (url, apiKey, body) = Assert.Single(_gemini.Requests);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent", url);
        Assert.Equal("test-key", apiKey);
        Assert.Equal("You are a careful reader.", (string?)body["systemInstruction"]!["parts"]![0]!["text"]);
        Assert.Equal("The posting.", (string?)body["contents"]![0]!["parts"]![0]!["text"]);

        var config = body["generationConfig"]!;
        Assert.Equal(0, (int)config["temperature"]!);
        Assert.Equal("application/json", (string?)config["responseMimeType"]);
        // Gemini wants type names in capitals, at every level.
        Assert.Equal("OBJECT", (string?)config["responseSchema"]!["type"]);
        Assert.Equal("ARRAY", (string?)config["responseSchema"]!["properties"]!["requirements"]!["type"]);
        Assert.Equal("STRING", (string?)config["responseSchema"]!["properties"]!["requirements"]!["items"]!["type"]);
    }

    [Fact]
    public async Task Leaves_the_callers_schema_untouched_so_it_can_be_reused()
    {
        await Generate();

        Assert.Equal("object", (string?)Schema["type"]);
    }

    [Fact]
    public async Task Records_what_each_call_cost()
    {
        await Generate();
        await Generate();

        var usage = _usage.Summarise();
        Assert.NotNull(usage);
        Assert.Equal(("gemini-test", 2, 2 * StubGemini.InputTokens, 2 * StubGemini.OutputTokens),
            (usage.Model, usage.Calls, usage.InputTokens, usage.OutputTokens));
    }

    [Fact]
    public async Task Reports_the_model_as_unavailable_when_the_request_is_refused()
    {
        _gemini.Refuse = true;

        var error = await Assert.ThrowsAsync<LlmUnavailableException>(Generate);

        Assert.Equal("The model refused the request with status 400.", error.Message);
        Assert.Null(_usage.Summarise());
    }

    [Theory]
    [InlineData("""{ "candidates": [] }""")]                                                        // blocked or empty
    [InlineData("""{ "candidates": [{ "content": { "parts": [{ "text": "Sure! Here you go" }] } }] }""")]  // not JSON
    [InlineData("not even an envelope")]
    public async Task Reports_the_model_as_unavailable_when_the_answer_is_unusable(string responseBody)
    {
        _gemini.RawBody = responseBody;

        await Assert.ThrowsAsync<LlmUnavailableException>(Generate);
    }

    private Task<Answer> Generate()
    {
        var options = Options.Create(new LlmOptions { ApiKey = "test-key", Model = "gemini-test" });
        var client = new GeminiClient(new HttpClient(_gemini), options, _usage, NullLogger<GeminiClient>.Instance);
        return client.GenerateAsync<Answer>(
            new LlmPrompt("extract", "You are a careful reader.", "The posting.", Schema), CancellationToken.None);
    }
}

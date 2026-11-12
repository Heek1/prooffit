using System.Text.Json.Nodes;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Llm;

public class LlmOptions
{
    /// <summary>Empty means no model is configured and analyses use the keyword engine.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-flash-latest";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// The model could not produce a usable answer: it was unreachable, refused, or returned
/// something that is not the JSON asked for. Callers fall back to the keyword engine.
/// </summary>
public class LlmUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <param name="Purpose">What the call is for, used in logs: "extract" or "match".</param>
/// <param name="Schema">A JSON Schema the answer must follow.</param>
public record LlmPrompt(string Purpose, string System, string User, JsonNode Schema);

/// <summary>
/// A language model that answers with JSON of a given shape. The interface is all the
/// extractor and matcher know about a provider, so adding one means one new class.
/// </summary>
public interface ILlmClient
{
    string Model { get; }

    /// <exception cref="LlmUnavailableException">No usable answer came back.</exception>
    Task<T> GenerateAsync<T>(LlmPrompt prompt, CancellationToken ct);
}

/// <summary>
/// Adds up the model calls made while one analysis runs, so its report can say what it
/// cost. One instance per analysis (scoped).
/// </summary>
public class LlmUsageRecorder
{
    private string? _model;
    private int _calls, _inputTokens, _outputTokens;
    private TimeSpan _duration;

    public void Record(string model, int inputTokens, int outputTokens, TimeSpan duration)
    {
        _model = model;
        _calls++;
        _inputTokens += inputTokens;
        _outputTokens += outputTokens;
        _duration += duration;
    }

    /// <summary>Null when the analysis made no model calls.</summary>
    public LlmUsage? Summarise() => _model is null
        ? null
        : new LlmUsage(_model, _calls, _inputTokens, _outputTokens, (int)_duration.TotalMilliseconds);
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Llm;
using ProofFit.Api.Tests.Llm;

namespace ProofFit.Api.Tests;

/// <summary>Listens to ProofFit's own traces and metrics, the way an exporter would.</summary>
public sealed class TelemetryRecorder : IDisposable
{
    private readonly ActivityListener _activities;
    private readonly MeterListener _meters = new();

    public ConcurrentQueue<Activity> Activities { get; } = new();
    public ConcurrentQueue<(string Instrument, long Value, Dictionary<string, object?> Tags)> Measurements { get; } = new();

    public TelemetryRecorder()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Activities.Enqueue,
        };
        ActivitySource.AddActivityListener(_activities);

        _meters.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == Telemetry.Name) listener.EnableMeasurementEvents(instrument);
        };
        _meters.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            Measurements.Enqueue((instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        _meters.Start();
    }

    public void Dispose()
    {
        _activities.Dispose();
        _meters.Dispose();
    }
}

[Collection(ApiCollection.Name)]
public class AnalysisTelemetryTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Docker in production
        - PostgreSQL and Redis
        """;

    [Fact]
    public async Task An_analysis_is_traced_with_its_steps_and_counted_by_outcome()
    {
        using var recorder = new TelemetryRecorder();
        var client = factory.CreateClient();

        var analysis = await client.AnalyseAsync(["Backend developer", "Containerised services with Docker"], Vacancy);

        // The span ends after the analysis is saved, so it can land a moment after the API reports completion.
        Activity? run = null;
        for (var wait = 0; wait < 50 && run is null; wait++)
        {
            run = recorder.Activities.FirstOrDefault(a => a.OperationName == "analysis.run" && Equals(a.GetTagItem("prooffit.analysis.id"), analysis.Id));
            if (run is null) await Task.Delay(100);
        }

        Assert.NotNull(run);
        Assert.Equal("completed", run.GetTagItem("prooffit.analysis.outcome"));
        Assert.Equal("keyword", run.GetTagItem("prooffit.analysis.engine"));
        Assert.Equal(2, run.GetTagItem("prooffit.analysis.requirements"));
        Assert.Equal(["stage.ReadingVacancy", "stage.Matching"], run.Events.Select(e => e.Name));

        Assert.Contains(recorder.Measurements, m =>
            m.Instrument == "prooffit.analyses.finished" && Equals(m.Tags["outcome"], "completed") && Equals(m.Tags["engine"], "keyword"));
    }
}

public class ModelTelemetryTests
{
    [Fact]
    public async Task A_model_call_is_traced_with_the_model_and_its_token_usage()
    {
        using var recorder = new TelemetryRecorder();
        var gemini = new StubGemini { ExtractAnswer = """{ "requirements": ["Docker"] }""" };
        var options = Options.Create(new LlmOptions { ApiKey = "test-key", Model = "gemini-telemetry" });
        var client = new GeminiClient(new HttpClient(gemini), options, new LlmUsageRecorder(), NullLogger<GeminiClient>.Instance);
        var schema = JsonNode.Parse("""{ "type": "object", "properties": { "requirements": { "type": "array", "items": { "type": "string" } } } }""")!;

        await client.GenerateAsync<JsonObject>(new LlmPrompt("extract", "System.", "User.", schema), CancellationToken.None);

        var call = Assert.Single(recorder.Activities, a => Equals(a.GetTagItem("gen_ai.request.model"), "gemini-telemetry"));
        Assert.Equal("generate_content gemini-telemetry", call.DisplayName);
        Assert.Equal(ActivityKind.Client, call.Kind);
        Assert.Equal("extract", call.GetTagItem("prooffit.prompt"));
        Assert.Equal(StubGemini.InputTokens, call.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.Equal(StubGemini.OutputTokens, call.GetTagItem("gen_ai.usage.output_tokens"));

        Assert.Contains(recorder.Measurements, m => m.Instrument == "gen_ai.client.token.usage"
            && m.Value == StubGemini.InputTokens && Equals(m.Tags["gen_ai.token.type"], "input") && Equals(m.Tags["gen_ai.request.model"], "gemini-telemetry"));
    }
}

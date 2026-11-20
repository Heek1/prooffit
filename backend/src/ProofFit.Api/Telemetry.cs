using System.Diagnostics;
using System.Diagnostics.Metrics;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ProofFit.Api;

/// <summary>
/// The traces and metrics ProofFit emits about its own work, next to the ones ASP.NET Core,
/// HttpClient and Npgsql emit about theirs. Nothing from a resume or a vacancy is recorded:
/// only ids, counts, engines and durations.
/// </summary>
public static class Telemetry
{
    public const string Name = "ProofFit";

    public static readonly ActivitySource Source = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>Analyses that ended, by outcome ("completed" or "failed") and engine.</summary>
    public static readonly Counter<long> AnalysesFinished = Meter.CreateCounter<long>(
        "prooffit.analyses.finished", unit: "{analysis}", description: "Analyses that completed or failed.");

    public static readonly Histogram<double> AnalysisDuration = Meter.CreateHistogram<double>(
        "prooffit.analysis.duration", unit: "s", description: "Time from a worker taking an analysis on to its end.");

    /// <summary>Times the model could not be used and the keyword engine took over, by step.</summary>
    public static readonly Counter<long> ModelFallbacks = Meter.CreateCounter<long>(
        "prooffit.model.fallbacks", unit: "{fallback}", description: "Model calls that failed and were replaced by the keyword engine.");

    /// <summary>Tokens the model used, by direction ("input" or "output") and model.</summary>
    public static readonly Counter<long> ModelTokens = Meter.CreateCounter<long>(
        "gen_ai.client.token.usage", unit: "{token}", description: "Tokens sent to and received from the model.");

    /// <summary>
    /// Records traces and metrics, and sends them, with logs, over OTLP when
    /// OTEL_EXPORTER_OTLP_ENDPOINT is set. Without it they are collected for in-process
    /// listeners only, which costs little and changes nothing for a local run.
    /// </summary>
    public static WebApplicationBuilder AddTelemetry(this WebApplicationBuilder builder)
    {
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("prooffit-api"))
            .WithTracing(tracing => tracing
                .AddSource(Name)
                // Health checks are polled every few seconds and would drown out real requests.
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddMeter(Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
            otel.UseOtlpExporter();
        }

        return builder;
    }
}

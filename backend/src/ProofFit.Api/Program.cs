using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ProofFit.Api;
using ProofFit.Api.Data;
using ProofFit.Api.Features.Analyses;
using ProofFit.Api.Features.Analyses.Matching;
using ProofFit.Api.Features.GitHub;
using ProofFit.Api.Features.Llm;
using ProofFit.Api.Features.Resumes;
using ProofFit.Api.Features.Vacancies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));

// The connection string is read when the context is first resolved rather than here, so
// configuration added after this point (integration tests, container env) still applies.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

builder.Services.AddSingleton<PdfTextExtractor>();

// The model is called through its own HttpClient. Its timeouts are long because one call
// reads a whole resume and vacancy, and the sampling window must cover two attempts.
builder.Services.AddOptions<LlmOptions>().BindConfiguration("Llm");
builder.Services.AddHttpClient<GeminiClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
    .AddStandardResilienceHandler(resilience =>
    {
        resilience.Retry.MaxRetryAttempts = 2;
        resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(150);
    });
builder.Services.AddScoped<ILlmClient>(services => services.GetRequiredService<GeminiClient>());
builder.Services.AddScoped<LlmUsageRecorder>();

// Analyses use the model when one is configured and the keyword engine otherwise. The
// choice is made when an analysis runs, so configuration added later still applies.
builder.Services.AddSingleton<KeywordRequirementExtractor>();
builder.Services.AddSingleton<KeywordRequirementMatcher>();
builder.Services.AddScoped<LlmRequirementExtractor>();
builder.Services.AddScoped<LlmRequirementMatcher>();
builder.Services.AddScoped<IRequirementExtractor>(services => UsesModel(services)
    ? services.GetRequiredService<LlmRequirementExtractor>()
    : services.GetRequiredService<KeywordRequirementExtractor>());
builder.Services.AddScoped<IRequirementMatcher>(services => UsesModel(services)
    ? services.GetRequiredService<LlmRequirementMatcher>()
    : services.GetRequiredService<KeywordRequirementMatcher>());
builder.Services.AddScoped<AnalysisRunner>();
builder.Services.AddSingleton<AnalysisQueue>();
builder.Services.AddHostedService<AnalysisWorker>();

builder.Services.AddOptions<GitHubOptions>().BindConfiguration("GitHub");
builder.Services.AddHttpClient<GitHubClient>(client =>
    {
        // GitHub rejects requests that do not say who is asking.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ProofFit");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    })
    .AddStandardResilienceHandler();
builder.Services.AddScoped<GitHubScanner>();
builder.Services.AddScoped<IGitHubEvidenceSource, GitHubEvidenceSource>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// live: the process is up. ready: it can also reach the database.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapResumeEndpoints();
app.MapVacancyEndpoints();
app.MapAnalysisEndpoints();

app.Run();

static bool UsesModel(IServiceProvider services) =>
    services.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmOptions>>().Value.IsConfigured;

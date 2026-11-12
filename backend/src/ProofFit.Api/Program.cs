using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ProofFit.Api;
using ProofFit.Api.Data;
using ProofFit.Api.Features.Analyses;
using ProofFit.Api.Features.Analyses.Matching;
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

builder.Services.AddSingleton<IRequirementExtractor, KeywordRequirementExtractor>();
builder.Services.AddSingleton<IRequirementMatcher, KeywordRequirementMatcher>();
builder.Services.AddScoped<AnalysisRunner>();
builder.Services.AddSingleton<AnalysisQueue>();
builder.Services.AddHostedService<AnalysisWorker>();

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

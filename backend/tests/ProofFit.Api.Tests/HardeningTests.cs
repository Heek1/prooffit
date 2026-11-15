using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofFit.Api.Data;

namespace ProofFit.Api.Tests;

[Collection(ApiCollection.Name)]
public class HardeningTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Docker
        - Kubernetes in production
        - PostgreSQL and Redis
        """;

    [Fact]
    public async Task Every_table_has_row_level_security_enabled()
    {
        // A table without it is readable through a host's generated REST API. A migration
        // that adds a table and forgets to enable it fails here.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unprotected = await db.Database
            .SqlQuery<string>($"""
                SELECT relname AS "Value" FROM pg_class
                WHERE relnamespace = 'public'::regnamespace AND relkind = 'r' AND NOT relrowsecurity
                """)
            .ToListAsync();

        Assert.Empty(unprotected);
    }

    [Fact]
    public async Task A_client_that_starts_comparisons_too_fast_is_told_to_wait()
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimit:AnalysesPerTenMinutes", "1"))
            .CreateClient();
        var resume = await client.UploadResumeAsync("Backend developer", "Containerised services with Docker");
        var vacancy = await client.CreateVacancyAsync(Vacancy);
        var request = new { resumeId = resume.Id, vacancyId = vacancy.Id };

        var first = await client.PostAsJsonAsync("/api/analyses", request);
        var second = await client.PostAsJsonAsync("/api/analyses", request);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Too many requests. Wait a few minutes and try again.", problem?.Title);
    }

    [Fact]
    public async Task Reading_is_not_rate_limited()
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimit:UploadsPerMinute", "1"))
            .CreateClient();
        var resume = await client.UploadResumeAsync("Backend developer");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.GetAsync($"/api/resumes/{resume.Id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

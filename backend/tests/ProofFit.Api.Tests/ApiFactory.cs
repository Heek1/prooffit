using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProofFit.Api.Features.GitHub;
using ProofFit.Api.Tests.GitHub;
using Testcontainers.PostgreSql;

namespace ProofFit.Api.Tests;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container, so the tests exercise the
/// same migrations and SQL as production. Needs Docker. GitHub is the one thing replaced:
/// requests to it are answered by <see cref="GitHub"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

    /// <summary>The GitHub the API sees. Tests add the users they need.</summary>
    public StubGitHub GitHub { get; } = new();

    public Task InitializeAsync() => _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");

        builder.ConfigureTestServices(services =>
            services.AddHttpClient<GitHubClient>().ConfigurePrimaryHttpMessageHandler(() => GitHub));
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

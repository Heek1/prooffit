using Microsoft.Extensions.Options;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.GitHub;

namespace ProofFit.Api.Tests.GitHub;

public class GitHubScannerTests
{
    private readonly StubGitHub _gitHub = new();

    [Fact]
    public async Task Reports_each_skill_with_the_repositories_that_show_it_newest_first()
    {
        _gitHub.AddUser("octo-dev",
            new StubRepository("shop-api").With("Dockerfile").With("Program.cs").With("Orders.cs").With("Cart.cs"),
            new StubRepository("infra").With("Dockerfile").With("k8s/deployment.yaml"));

        var evidence = await Scanner().ScanAsync("octo-dev", CancellationToken.None);

        Assert.Equal(("octo-dev", 2), (evidence.Login, evidence.RepositoriesScanned));
        Assert.Equal(
        [
            ("G1", "C#", "shop-api"),
            ("G2", "Docker", "shop-api, infra"),
            ("G3", "Kubernetes", "infra"),
        ], evidence.Facts.Select(fact => (fact.Id, fact.Skill, string.Join(", ", fact.Repositories.Select(r => r.Name)))));
        Assert.Equal(
            new RepositoryRef("infra", "https://github.com/octo-dev/infra"),
            evidence.Facts[2].Repositories[0]);
    }

    [Fact]
    public async Task Reads_dependency_manifests_even_at_paths_with_spaces()
    {
        _gitHub.AddUser("octo-dev", new StubRepository("prompts")
            .With("Prompt Manager/Prompt Manager.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"></Project>"""));

        var evidence = await Scanner().ScanAsync("octo-dev", CancellationToken.None);

        Assert.Contains("ASP.NET Core", evidence.Facts.Select(fact => fact.Skill));
    }

    [Fact]
    public async Task Skips_forks_because_they_are_someone_elses_code()
    {
        _gitHub.AddUser("octo-dev",
            new StubRepository("linux") { Fork = true }.With("Dockerfile"),
            new StubRepository("notes").With("notes.py"));

        var evidence = await Scanner().ScanAsync("octo-dev", CancellationToken.None);

        Assert.Equal(1, evidence.RepositoriesScanned);
        Assert.Equal(["Python"], evidence.Facts.Select(fact => fact.Skill));
    }

    [Fact]
    public async Task Scans_only_the_most_recent_repositories_up_to_the_limit()
    {
        _gitHub.AddUser("octo-dev",
            new StubRepository("newest").With("a.py"),
            new StubRepository("older").With("b.py"),
            new StubRepository("oldest").With("Dockerfile"));

        var evidence = await Scanner(new GitHubOptions { MaxRepositories = 2 }).ScanAsync("octo-dev", CancellationToken.None);

        Assert.Equal(2, evidence.RepositoriesScanned);
        Assert.DoesNotContain("Docker", evidence.Facts.Select(fact => fact.Skill));
    }

    [Fact]
    public async Task Fails_with_a_clear_reason_for_an_unknown_user()
    {
        var error = await Assert.ThrowsAsync<GitHubUnavailableException>(
            () => Scanner().ScanAsync("nobody-here", CancellationToken.None));

        Assert.Equal("There is no GitHub user named nobody-here.", error.Message);
    }

    [Fact]
    public async Task Fails_with_a_clear_reason_when_the_request_limit_is_reached()
    {
        _gitHub.AddUser("octo-dev", new StubRepository("notes").With("notes.py"));
        _gitHub.RateLimited = true;

        var error = await Assert.ThrowsAsync<GitHubUnavailableException>(
            () => Scanner().ScanAsync("octo-dev", CancellationToken.None));

        Assert.Equal("GitHub's hourly request limit was reached. Try again later.", error.Message);
    }

    [Fact]
    public async Task Sends_the_token_to_the_api_but_not_with_raw_file_downloads()
    {
        _gitHub.AddUser("octo-dev", new StubRepository("web").With("package.json", """{ "dependencies": { "react": "19" } }"""));

        await Scanner(new GitHubOptions { Token = "test-token" }).ScanAsync("octo-dev", CancellationToken.None);

        Assert.All(_gitHub.Requests, request =>
            Assert.Equal(request.Url.StartsWith("https://api.github.com/"), request.Authorized));
        Assert.Contains(_gitHub.Requests, request => request.Url.StartsWith("https://raw.githubusercontent.com/"));
    }

    [Theory]
    [InlineData("Heek1", "Heek1")]
    [InlineData("  @octo-dev ", "octo-dev")]
    [InlineData("https://github.com/octo-dev/", "octo-dev")]
    [InlineData("github.com/octo-dev", "octo-dev")]
    [InlineData("not a login", null)]
    [InlineData("-leading-hyphen", null)]
    [InlineData("double--hyphen", null)]
    [InlineData("https://github.com/octo-dev/some-repo", null)]
    [InlineData("", null)]
    public void Parses_a_login_as_people_type_it(string input, string? expected)
    {
        Assert.Equal(expected, GitHubClient.ParseLogin(input));
    }

    private GitHubScanner Scanner(GitHubOptions? options = null)
    {
        var configured = Options.Create(options ?? new GitHubOptions());
        return new GitHubScanner(new GitHubClient(new HttpClient(_gitHub), configured), configured);
    }
}

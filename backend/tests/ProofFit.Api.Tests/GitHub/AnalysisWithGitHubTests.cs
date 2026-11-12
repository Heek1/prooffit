using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Tests.GitHub;

[Collection(ApiCollection.Name)]
public class AnalysisWithGitHubTests(ApiFactory factory)
{
    private const string Vacancy = """
        Requirements:
        - Docker
        - Kubernetes in production
        - Kafka
        """;

    // The resume shows Docker and nothing else the vacancy asks for.
    private static readonly string[] Resume = ["Backend developer", "Containerised services with Docker"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Public_code_proves_a_requirement_the_resume_does_not_mention()
    {
        factory.GitHub.AddUser("kube-dev",
            new StubRepository("infra").With("k8s/deployment.yaml").With("Dockerfile"),
            new StubRepository("notes").With("notes.py"));

        var analysis = await _client.AnalyseAsync(Resume, Vacancy, "https://github.com/kube-dev");

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal("kube-dev", analysis.GitHubLogin);

        var result = analysis.Result!;
        Assert.Null(result.GitHubNote);
        Assert.Equal(("kube-dev", 2), (result.GitHub!.Login, result.GitHub.RepositoriesScanned));

        Assert.Equal(
            [MatchStatus.Confirmed, MatchStatus.Confirmed, MatchStatus.Missing],
            result.Requirements.Select(r => r.Status));

        // Kubernetes rests on GitHub alone, and the fact it cites names the repository.
        var kubernetes = result.Requirements[1];
        Assert.Empty(kubernetes.ResumeLines);
        var fact = result.GitHub.Facts.Single(f => f.Id == Assert.Single(kubernetes.GitHubFacts));
        Assert.Equal("Kubernetes", fact.Skill);
        Assert.Equal(new RepositoryRef("infra", "https://github.com/kube-dev/infra"), Assert.Single(fact.Repositories));

        // Docker is shown by both sources, and both are cited.
        var docker = result.Requirements[0];
        Assert.Equal([2], docker.ResumeLines);
        Assert.Equal("Docker", result.GitHub.Facts.Single(f => f.Id == Assert.Single(docker.GitHubFacts)).Skill);
    }

    [Fact]
    public async Task A_profile_is_scanned_once_and_then_served_from_the_cache()
    {
        factory.GitHub.AddUser("cached-dev", new StubRepository("infra").With("Dockerfile"));

        var first = await _client.AnalyseAsync(Resume, Vacancy, "cached-dev");
        var requestsAfterFirst = RequestsFor("cached-dev");
        // Logins are case-insensitive on GitHub, so a different spelling is the same profile.
        var second = await _client.AnalyseAsync(Resume, Vacancy, "CACHED-DEV");

        Assert.NotEqual(0, requestsAfterFirst);
        Assert.Equal(requestsAfterFirst, RequestsFor("cached-dev"));
        Assert.Equal(first.Result!.GitHub, second.Result!.GitHub, GitHubEvidenceComparer.Instance);
    }

    [Fact]
    public async Task The_analysis_still_completes_when_the_github_user_does_not_exist()
    {
        var analysis = await _client.AnalyseAsync(Resume, Vacancy, "ghost-user-404");

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        var result = analysis.Result!;
        Assert.Null(result.GitHub);
        Assert.Equal("There is no GitHub user named ghost-user-404.", result.GitHubNote);
        // The resume alone still proves Docker.
        Assert.Equal(MatchStatus.Confirmed, result.Requirements[0].Status);
        Assert.Equal(MatchStatus.Missing, result.Requirements[1].Status);
    }

    [Fact]
    public async Task Create_rejects_something_that_cannot_be_a_github_username()
    {
        var resume = await _client.UploadResumeAsync(Resume);
        var vacancy = await _client.CreateVacancyAsync(Vacancy);

        var response = await _client.PostAsJsonAsync("/api/analyses",
            new { resumeId = resume.Id, vacancyId = vacancy.Id, gitHubLogin = "not a login!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["gitHubLogin"], problem?.Errors.Keys);
    }

    private int RequestsFor(string login) =>
        factory.GitHub.Requests.Count(request => request.Url.Contains($"/{login}/", StringComparison.OrdinalIgnoreCase));

    // Records compare their list members by reference, so evidence read back from two
    // responses needs comparing by content.
    private sealed class GitHubEvidenceComparer : IEqualityComparer<GitHubEvidence?>
    {
        public static readonly GitHubEvidenceComparer Instance = new();

        public bool Equals(GitHubEvidence? x, GitHubEvidence? y) =>
            x is not null && y is not null &&
            (x.Login, x.RepositoriesScanned) == (y.Login, y.RepositoriesScanned) &&
            x.Facts.Select(Describe).SequenceEqual(y.Facts.Select(Describe));

        public int GetHashCode(GitHubEvidence? evidence) => evidence?.Login.GetHashCode() ?? 0;

        private static string Describe(GitHubFact fact) =>
            $"{fact.Id} {fact.Skill} {string.Join(',', fact.Repositories.Select(r => r.Url))}";
    }
}

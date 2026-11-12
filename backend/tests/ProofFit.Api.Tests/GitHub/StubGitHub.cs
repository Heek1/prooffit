using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace ProofFit.Api.Tests.GitHub;

/// <summary>A repository as the stub serves it: file paths, and contents for the files that have any.</summary>
public sealed class StubRepository(string name)
{
    public string Name => name;
    public bool Fork { get; init; }
    public Dictionary<string, string?> Files { get; } = [];

    public StubRepository With(string path, string? content = null)
    {
        Files[path] = content;
        return this;
    }
}

/// <summary>
/// Stands in for api.github.com and raw.githubusercontent.com, so tests neither need the
/// network nor spend a real request limit. Unknown URLs answer 404, as GitHub does.
/// </summary>
public sealed class StubGitHub : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, string> _bodies = new();

    /// <summary>Every request received: the URL without its query, and whether it carried a token.</summary>
    public ConcurrentQueue<(string Url, bool Authorized)> Requests { get; } = new();

    /// <summary>When set, every request is refused the way GitHub refuses one over the hourly limit.</summary>
    public bool RateLimited { get; set; }

    /// <summary>Registers a user. Repositories are listed newest first, in the order given.</summary>
    public void AddUser(string login, params StubRepository[] repositories)
    {
        var pushedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        _bodies[$"https://api.github.com/users/{login}/repos"] = JsonSerializer.Serialize(repositories.Select((repository, index) => new
        {
            name = repository.Name,
            html_url = $"https://github.com/{login}/{repository.Name}",
            default_branch = "main",
            fork = repository.Fork,
            size = repository.Files.Count,
            pushed_at = pushedAt.AddDays(-index),
        }));

        foreach (var repository in repositories)
        {
            _bodies[$"https://api.github.com/repos/{login}/{repository.Name}/git/trees/main"] = JsonSerializer.Serialize(new
            {
                tree = repository.Files.Keys.Select(path => new { path, type = "blob" }),
            });

            foreach (var (path, content) in repository.Files)
            {
                if (content is null) continue;
                var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
                _bodies[$"https://raw.githubusercontent.com/{login}/{repository.Name}/main/{escaped}"] = content;
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.GetLeftPart(UriPartial.Path);
        Requests.Enqueue((url, request.Headers.Authorization is not null));

        if (RateLimited)
        {
            var refused = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
            refused.Headers.Add("x-ratelimit-remaining", "0");
            return Task.FromResult(refused);
        }

        var response = _bodies.TryGetValue(url, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        return Task.FromResult(response);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ProofFit.Api.Features.GitHub;

public class GitHubOptions
{
    /// <summary>
    /// Optional token with public read access. Without one GitHub allows 60 API requests an
    /// hour per IP address, which is enough for two or three profiles.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>How many of a user's most recently pushed repositories are scanned.</summary>
    public int MaxRepositories { get; set; } = 20;

    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>GitHub evidence could not be gathered. The message is shown to the user as is.</summary>
public class GitHubUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public record GitHubRepository(
    string Name, string HtmlUrl, string? DefaultBranch, bool Fork, int Size, DateTimeOffset? PushedAt);

/// <summary>The few read-only GitHub calls a profile scan needs.</summary>
public partial class GitHubClient(HttpClient http, IOptions<GitHubOptions> options)
{
    private const int MaxFileBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// Accepts a login as people type it: "Heek1", "@Heek1" or a profile URL. Returns null
    /// when what is left is not a possible GitHub login.
    /// </summary>
    public static string? ParseLogin(string? input)
    {
        var login = ProfilePrefix().Replace(input?.Trim() ?? "", "").TrimEnd('/');
        return LoginShape().IsMatch(login) ? login : null;
    }

    /// <summary>A user's own repositories, most recently pushed first.</summary>
    public async Task<IReadOnlyList<GitHubRepository>> ListRepositoriesAsync(string login, CancellationToken ct)
    {
        using var response = await GetAsync(
            $"https://api.github.com/users/{login}/repos?per_page=100&sort=pushed&type=owner", authorize: true, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new GitHubUnavailableException($"There is no GitHub user named {login}.");
        if (!response.IsSuccessStatusCode)
            throw new GitHubUnavailableException("GitHub could not list this user's repositories.");

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<List<GitHubRepository>>(body, Json, ct) ?? [];
    }

    /// <summary>Paths of every file in a repository, or null when it has no readable tree.</summary>
    public async Task<IReadOnlyList<string>?> GetFilePathsAsync(string login, GitHubRepository repository, CancellationToken ct)
    {
        if (repository.DefaultBranch is null) return null;

        using var response = await GetAsync(
            $"https://api.github.com/repos/{login}/{Uri.EscapeDataString(repository.Name)}/git/trees/" +
            $"{Uri.EscapeDataString(repository.DefaultBranch)}?recursive=1", authorize: true, ct);
        if (!response.IsSuccessStatusCode) return null;

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var listing = await JsonSerializer.DeserializeAsync<TreeListing>(body, Json, ct);
        return listing?.Tree.Where(entry => entry.Type == "blob").Select(entry => entry.Path).ToList();
    }

    /// <summary>The text of one file, or null when it cannot be read or is too large to be a manifest.</summary>
    public async Task<string?> GetFileAsync(string login, GitHubRepository repository, string path, CancellationToken ct)
    {
        // Raw file downloads do not count against the API request limit, and public files
        // need no token, so none is sent.
        var segments = new[] { login, repository.Name, repository.DefaultBranch! }.Concat(path.Split('/'));
        var url = "https://raw.githubusercontent.com/" + string.Join('/', segments.Select(Uri.EscapeDataString));

        using var response = await GetAsync(url, authorize: false, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxFileBytes) return null;

        var text = await response.Content.ReadAsStringAsync(ct);
        return text.Length <= MaxFileBytes ? text : null;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, bool authorize, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (authorize && !string.IsNullOrWhiteSpace(options.Value.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        // Network failures and timeouts surface as several exception types; unless the
        // caller cancelled, they all mean GitHub is out of reach.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            throw new GitHubUnavailableException("GitHub could not be reached.", ex);
        }

        if (IsRateLimited(response))
        {
            response.Dispose();
            throw new GitHubUnavailableException("GitHub's hourly request limit was reached. Try again later.");
        }
        return response;
    }

    private static bool IsRateLimited(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.TooManyRequests ||
        (response.StatusCode == HttpStatusCode.Forbidden &&
         response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) &&
         remaining.FirstOrDefault() == "0");

    private record TreeListing(List<TreeEntry> Tree);

    private record TreeEntry(string Path, string Type);

    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?github\.com/|^@", RegexOptions.IgnoreCase)]
    private static partial Regex ProfilePrefix();

    // GitHub's own rule: letters, digits and single hyphens, not at either end, up to 39.
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$")]
    private static partial Regex LoginShape();
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;

namespace ProofFit.Api.Features.GitHub;

/// <summary>Reads a user's public repositories and reports which skills they show, and where.</summary>
public class GitHubScanner(GitHubClient client, IOptions<GitHubOptions> options)
{
    private const int MaxConcurrentRepositories = 4;

    /// <exception cref="GitHubUnavailableException">Nothing could be read: no such user, no network, or the request limit.</exception>
    public async Task<GitHubEvidence> ScanAsync(string login, CancellationToken ct)
    {
        var repositories = (await client.ListRepositoriesAsync(login, ct))
            .Where(repository => !repository.Fork && repository.Size > 0)
            .OrderByDescending(repository => repository.PushedAt)
            .Take(options.Value.MaxRepositories)
            .ToList();

        var scanned = new ConcurrentDictionary<GitHubRepository, IReadOnlySet<string>>();
        GitHubUnavailableException? interrupted = null;

        var parallel = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentRepositories, CancellationToken = ct };
        await Parallel.ForEachAsync(repositories, parallel, async (repository, token) =>
        {
            // Once GitHub has turned one request away, asking again only spends the limit.
            if (interrupted is not null) return;
            try
            {
                if (await ScanRepositoryAsync(login, repository, token) is { } skills) scanned[repository] = skills;
            }
            catch (GitHubUnavailableException ex)
            {
                interrupted = ex;
            }
        });

        // A partial scan is still true: every fact in it was seen. A scan that read nothing
        // is not evidence of anything, so it is reported as a failure instead.
        if (scanned.IsEmpty && interrupted is not null) throw interrupted;

        // One fact per skill, in catalog order, listing its repositories newest first.
        var facts = SkillCatalog.All
            .Select(skill => (skill.Name, Repositories: repositories
                .Where(repository => scanned.TryGetValue(repository, out var skills) && skills.Contains(skill.Name))
                .Select(repository => new RepositoryRef(repository.Name, repository.HtmlUrl))
                .ToList()))
            .Where(entry => entry.Repositories.Count > 0)
            .Select((entry, index) => new GitHubFact($"G{index + 1}", entry.Name, entry.Repositories))
            .ToList();

        return new GitHubEvidence(login, scanned.Count, facts);
    }

    private async Task<IReadOnlySet<string>?> ScanRepositoryAsync(string login, GitHubRepository repository, CancellationToken ct)
    {
        var paths = await client.GetFilePathsAsync(login, repository, ct);
        if (paths is null) return null;

        var manifests = new Dictionary<string, string>();
        foreach (var path in RepositorySignals.ManifestsToRead(paths))
        {
            if (await client.GetFileAsync(login, repository, path, ct) is { } content) manifests[path] = content;
        }

        return RepositorySignals.Detect(paths, manifests);
    }
}

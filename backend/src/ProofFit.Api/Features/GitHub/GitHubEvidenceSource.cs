using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.GitHub;

public interface IGitHubEvidenceSource
{
    /// <exception cref="GitHubUnavailableException">The profile could not be read.</exception>
    Task<GitHubEvidence> GetAsync(string login, CancellationToken ct);
}

/// <summary>
/// Serves a profile's evidence from the database while it is fresh and scans GitHub
/// otherwise. A scan costs a request per repository against a small hourly limit, and a
/// profile changes slowly, so a day-old answer is the right trade.
/// </summary>
public class GitHubEvidenceSource(AppDbContext db, GitHubScanner scanner, IOptions<GitHubOptions> options) : IGitHubEvidenceSource
{
    public async Task<GitHubEvidence> GetAsync(string login, CancellationToken ct)
    {
        var key = login.ToLowerInvariant();
        var cached = await db.GitHubProfiles.SingleOrDefaultAsync(profile => profile.Login == key, ct);
        if (cached is not null && cached.FetchedAt >= Timestamp.Now() - options.Value.CacheLifetime)
            return cached.Evidence;

        var evidence = await scanner.ScanAsync(login, ct);

        var profile = cached ?? db.GitHubProfiles.Add(new GitHubProfile { Login = key, Evidence = evidence }).Entity;
        profile.Evidence = evidence;
        profile.FetchedAt = Timestamp.Now();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another analysis stored the same profile first. Its copy is as good as this
            // one, and the entry must not stay queued in a context the caller keeps using.
            db.Entry(profile).State = EntityState.Detached;
        }

        return evidence;
    }
}

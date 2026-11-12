namespace ProofFit.Api.Domain;

public record RepositoryRef(string Name, string Url);

/// <summary>
/// One thing a candidate's public code shows: a skill, and the repositories it was found
/// in. Facts are gathered by plain code from files and dependency lists, so each one can
/// be checked by opening the repository.
/// </summary>
/// <param name="Id">Stable within one profile: "G1", "G2", and so on.</param>
/// <param name="Skill">A name from the skill catalog.</param>
public record GitHubFact(string Id, string Skill, IReadOnlyList<RepositoryRef> Repositories);

public record GitHubEvidence(string Login, int RepositoriesScanned, IReadOnlyList<GitHubFact> Facts);

/// <summary>A scanned profile, kept so repeat analyses do not spend GitHub's request limit.</summary>
public class GitHubProfile
{
    /// <summary>Lowercase, since GitHub logins are case-insensitive.</summary>
    public required string Login { get; set; }
    public required GitHubEvidence Evidence { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}

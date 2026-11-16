using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<GitHubProfile> GitHubProfiles => Set<GitHubProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resume>().Property(r => r.FileName).HasMaxLength(Limits.FileNameLength);
        modelBuilder.Entity<Vacancy>().Property(v => v.Title).HasMaxLength(Limits.VacancyTitleLength);

        modelBuilder.Entity<Analysis>(analysis =>
        {
            // Stored as text so the rows read plainly and adding a stage later is not a
            // renumbering of existing data.
            analysis.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            analysis.Property(a => a.Stage).HasConversion<string>().HasMaxLength(30);
            analysis.Property(a => a.Engine).HasMaxLength(100);
            analysis.Property(a => a.GitHubLogin).HasMaxLength(Limits.GitHubLoginLength);

            analysis.Property(a => a.Result)
                .HasColumnType("jsonb")
                .HasConversion(
                    result => JsonSerializer.Serialize(result, ApiJson.Options),
                    json => JsonSerializer.Deserialize<AnalysisResult>(json, ApiJson.Options)!);

            analysis.Property(a => a.GivenRequirements)
                .HasColumnType("jsonb")
                .HasConversion(
                    given => JsonSerializer.Serialize(given, ApiJson.Options),
                    json => JsonSerializer.Deserialize<List<GivenRequirement>>(json, ApiJson.Options),
                    // Set once when the analysis is made and never changed, so comparing by
                    // content is only needed for change tracking to be correct.
                    new ValueComparer<List<GivenRequirement>?>(
                        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                        list => list == null ? 0 : list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                        list => list == null ? null : list.ToList()));

            // The worker looks for unfinished analyses by status on every start.
            analysis.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<GitHubProfile>(profile =>
        {
            profile.HasKey(p => p.Login);
            profile.Property(p => p.Login).HasMaxLength(Limits.GitHubLoginLength);

            profile.Property(p => p.Evidence)
                .HasColumnType("jsonb")
                .HasConversion(
                    evidence => JsonSerializer.Serialize(evidence, ApiJson.Options),
                    json => JsonSerializer.Deserialize<GitHubEvidence>(json, ApiJson.Options)!);
        });
    }
}

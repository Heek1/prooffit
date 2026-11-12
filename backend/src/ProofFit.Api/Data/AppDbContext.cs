using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Analysis> Analyses => Set<Analysis>();

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

            analysis.Property(a => a.Result)
                .HasColumnType("jsonb")
                .HasConversion(
                    result => JsonSerializer.Serialize(result, ApiJson.Options),
                    json => JsonSerializer.Deserialize<AnalysisResult>(json, ApiJson.Options)!);

            // The worker looks for unfinished analyses by status on every start.
            analysis.HasIndex(a => a.Status);
        });
    }
}

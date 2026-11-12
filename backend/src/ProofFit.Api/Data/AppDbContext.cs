using Microsoft.EntityFrameworkCore;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The matching stage stores embeddings in vector columns. Enabling the extension
        // in the first migration means every database starts out able to hold them.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Resume>().Property(r => r.FileName).HasMaxLength(Limits.FileNameLength);
        modelBuilder.Entity<Vacancy>().Property(v => v.Title).HasMaxLength(Limits.VacancyTitleLength);
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofFit.Api.Data.Migrations
{
    /// <summary>
    /// Turns on row level security for every table, with no policies. The application
    /// connects as the tables' owner, which is exempt, so nothing changes for it. Any other
    /// role is denied. That matters on hosts such as Supabase, which expose the public schema
    /// through a generated REST API to roles other than the owner.
    ///
    /// Every later migration that creates a table has to enable it for that table too;
    /// a test fails when one is missed.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        private static readonly string[] Tables =
            ["Resumes", "Vacancies", "Analyses", "GitHubProfiles", "__EFMigrationsHistory"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.Sql($"""ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.Sql($"""ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;""");
        }
    }
}

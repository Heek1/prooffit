using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofFit.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubLogin",
                table: "Analyses",
                type: "character varying(39)",
                maxLength: 39,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GitHubProfiles",
                columns: table => new
                {
                    Login = table.Column<string>(type: "character varying(39)", maxLength: 39, nullable: false),
                    Evidence = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubProfiles", x => x.Login);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubProfiles");

            migrationBuilder.DropColumn(
                name: "GitHubLogin",
                table: "Analyses");
        }
    }
}

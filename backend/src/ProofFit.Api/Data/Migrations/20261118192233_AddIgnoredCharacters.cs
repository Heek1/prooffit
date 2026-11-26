using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofFit.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIgnoredCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IgnoredCharacters",
                table: "Resumes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoredCharacters",
                table: "Resumes");
        }
    }
}

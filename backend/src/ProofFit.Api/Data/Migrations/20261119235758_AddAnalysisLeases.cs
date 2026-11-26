using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofFit.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "Analyses",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Analyses");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "Analyses");
        }
    }
}

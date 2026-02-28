using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalLeagueId",
                table: "Tournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalSeason",
                table: "Tournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalFixtureId",
                table: "Games",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_ExternalFixtureId",
                table: "Games",
                column: "ExternalFixtureId",
                unique: true,
                filter: "\"ExternalFixtureId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_ExternalFixtureId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ExternalLeagueId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ExternalSeason",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ExternalFixtureId",
                table: "Games");
        }
    }
}

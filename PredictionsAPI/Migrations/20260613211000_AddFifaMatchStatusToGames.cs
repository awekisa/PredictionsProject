using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFifaMatchStatusToGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FifaMatchStatus",
                table: "Games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FifaMatchTime",
                table: "Games",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FifaMatchStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "FifaMatchTime",
                table: "Games");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamShortNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwayTeamShort",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeTeamShort",
                table: "Games",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayTeamShort",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeTeamShort",
                table: "Games");
        }
    }
}

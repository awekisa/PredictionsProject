using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmblemUrl",
                table: "Tournaments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwayCrestUrl",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeCrestUrl",
                table: "Games",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmblemUrl",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "AwayCrestUrl",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeCrestUrl",
                table: "Games");
        }
    }
}

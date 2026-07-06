using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddGamePredictionDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PredictionDeadline",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Games\" SET \"PredictionDeadline\" = \"StartTime\" WHERE \"PredictionDeadline\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PredictionDeadline",
                table: "Games");
        }
    }
}

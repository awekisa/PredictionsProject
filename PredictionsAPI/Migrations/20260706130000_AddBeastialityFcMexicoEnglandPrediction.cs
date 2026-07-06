using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PredictionsAPI.Data;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706130000_AddBeastialityFcMexicoEnglandPrediction")]
    public partial class AddBeastialityFcMexicoEnglandPrediction : Migration
    {
        private const int MexicoEnglandGameId = 2398;
        private const string BeastialityFcUserId = "5a414fbe-2c41-4a2f-803b-1729682d4c76";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "Predictions" ("GameId", "UserId", "HomeGoals", "AwayGoals", "CreatedAt")
                SELECT g."Id", u."Id", 1, 1, NOW()
                FROM "Games" g
                JOIN "AspNetUsers" u ON u."Id" = '{BeastialityFcUserId}'
                WHERE g."Id" = {MexicoEnglandGameId}
                  AND g."HomeTeam" = 'Mexico'
                  AND g."AwayTeam" = 'England'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Predictions" p
                      WHERE p."GameId" = g."Id"
                        AND p."UserId" = u."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM "Predictions"
                WHERE "GameId" = {MexicoEnglandGameId}
                  AND "UserId" = '{BeastialityFcUserId}'
                  AND "HomeGoals" = 1
                  AND "AwayGoals" = 1;
                """);
        }
    }
}

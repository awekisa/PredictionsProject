using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PredictionsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccessTokenExpiresAt",
                table: "McpAccessTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OAuthClientId",
                table: "McpAccessTokens",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resource",
                table: "McpAccessTokens",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "McpAccessTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "McpOAuthClients",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RedirectUris = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpOAuthRefreshTokens",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthRefreshTokens", x => x.Hash);
                    table.ForeignKey(
                        name: "FK_McpOAuthRefreshTokens_McpAccessTokens_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "McpAccessTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpOAuthRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    State = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Challenge = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: false),
                    Scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Decided = table.Column<bool>(type: "boolean", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpOAuthRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRefreshTokens_ConnectionId",
                table: "McpOAuthRefreshTokens",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRequests_CodeHash",
                table: "McpOAuthRequests",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRequests_ExpiresAt",
                table: "McpOAuthRequests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRequests_UserId",
                table: "McpOAuthRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpOAuthClients");

            migrationBuilder.DropTable(
                name: "McpOAuthRefreshTokens");

            migrationBuilder.DropTable(
                name: "McpOAuthRequests");

            migrationBuilder.DropColumn(
                name: "AccessTokenExpiresAt",
                table: "McpAccessTokens");

            migrationBuilder.DropColumn(
                name: "OAuthClientId",
                table: "McpAccessTokens");

            migrationBuilder.DropColumn(
                name: "Resource",
                table: "McpAccessTokens");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "McpAccessTokens");
        }
    }
}

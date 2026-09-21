using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PredictionsAPI.Data;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class McpPostgresTests
{
    [PostgresFact]
    public async Task MigrationRoundTrip_Persistence_LastUsed_Revocation_AndOwnerCascade()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES")!;
        var database = $"mcp_tokens_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Users.Add(DbContextFactory.MakeUser("alice", "Alice"));
                db.Roles.Add(new IdentityRole { Id = "admin", Name = "Admin", NormalizedName = "ADMIN" });
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = "alice", RoleId = "admin" });
                await db.SaveChangesAsync();
            }
            string credential;
            Guid tokenId;
            await using (var db = new AppDbContext(options))
            {
                var result = (await new McpAccessTokenService(db, TimeProvider.System).CreateAsync("alice", new()
                {
                    Name = "Postgres test", Scopes = [McpScopes.AppRead, McpScopes.AdminRead]
                }))!;
                credential = result.Token;
                tokenId = result.Connection.Id;
            }
            await using (var db = new AppDbContext(options))
            {
                var service = new McpAccessTokenService(db, TimeProvider.System);
                var principal = (await service.AuthenticateAsync(credential))!;
                principal.Should().NotBeNull();
                (await service.HasPermissionAsync(principal, McpScopes.AdminRead)).Should().BeTrue();
                var stored = await db.McpAccessTokens.AsNoTracking().SingleAsync();
                stored.TokenHash.Should().HaveLength(64).And.NotBe(credential);
                stored.LastUsedAt.Should().NotBeNull();
                stored.Scopes.Should().BeEquivalentTo(McpScopes.AppRead, McpScopes.AdminRead);
                await using var otherRequest = new AppDbContext(options);
                await new McpAccessTokenService(otherRequest, TimeProvider.System).RevokeAsync("alice", tokenId);
                (await service.HasPermissionAsync(principal, McpScopes.AppRead)).Should().BeFalse();
                (await service.AuthenticateAsync(credential)).Should().BeNull();
                await db.Users.Where(u => u.Id == "alice").ExecuteDeleteAsync();
                (await db.McpAccessTokens.AnyAsync()).Should().BeFalse();
                (await service.AuthenticateAsync(credential)).Should().BeNull();
                var previousMigration = (await db.Database.GetAppliedMigrationsAsync()).Reverse().Skip(1).First();
                await db.GetService<IMigrator>().MigrateAsync(previousMigration);
                (await db.Database.GetAppliedMigrationsAsync()).Should().NotContain(m => m.EndsWith("AddMcpAccessTokens"));
                await db.Database.MigrateAsync();
                (await db.McpAccessTokens.AnyAsync()).Should().BeFalse();
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES")))
                Skip = "Set MCP_TEST_POSTGRES to a disposable PostgreSQL server with CREATE DATABASE permission.";
        }
    }
}

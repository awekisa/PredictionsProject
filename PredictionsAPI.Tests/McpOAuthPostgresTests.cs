using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using PredictionsAPI.Data;
using PredictionsAPI.OAuth;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PredictionsAPI.Tests;

public class McpOAuthPostgresTests
{
    [PostgresFact]
    public async Task ConcurrentCodeAndRefreshExchange_RevocationRace_AndCascade_AreAtomic()
    {
        var adminString = Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES")!;
        var database = $"mcp_oauth_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await cmd.ExecuteNonQueryAsync();
        var cs = new NpgsqlConnectionStringBuilder(adminString) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options;
        var settings = Options.Create(new McpOAuthOptions());
        var client = "test-client";
        var verifier = new string('a', 43);
        var code = McpOAuthService.Secret();
        McpOAuthService Service(AppDbContext db) => new(db, TimeProvider.System, settings, new ClientMetadataResolver());
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Database.HasPendingModelChanges().Should().BeFalse();
                db.McpOAuthClients.Add(new() { Id = client, Name = "Claude", RedirectUris = settings.Value.AllowedRedirectUris });
                db.Users.Add(DbContextFactory.MakeUser("alice", "Alice"));
                db.McpOAuthRequests.Add(new() { Id = McpOAuthService.Secret(), ClientId = client, ClientName = "Claude", UserId = "alice",
                    Resource = settings.Value.Resource, RedirectUri = settings.Value.AllowedRedirectUris[0], Scopes = ["app:read"],
                    Challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
                    Decided = true, CodeHash = McpOAuthService.Hash(code), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2) });
                await db.SaveChangesAsync();
            }
            var barrier = new SaveBarrier(2);
            var racingOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).AddInterceptors(barrier).Options;
            async Task<JsonElement?> Exchange(bool refresh, string credential)
            {
                await using var db = new AppDbContext(racingOptions);
                try { return JsonSerializer.SerializeToElement(await Service(db).ExchangeAsync(refresh ? "refresh_token" : "authorization_code",
                    client, settings.Value.Resource, refresh ? null : credential, settings.Value.AllowedRedirectUris[0], verifier,
                    refresh ? credential : null, null, default)); }
                catch (OAuthException e) { e.Error.Should().Be("invalid_grant"); return null; }
            }
            var results = await Task.WhenAll(Exchange(false, code), Exchange(false, code));
            results.Count(x => x.HasValue).Should().Be(1);
            var token = results.Single(x => x.HasValue)!.Value;
            await using (var db = new AppDbContext(options))
            {
                (await db.McpAccessTokens.CountAsync()).Should().Be(1);
                (await db.McpOAuthRefreshTokens.CountAsync()).Should().Be(1);
                var record = await db.McpAccessTokens.SingleAsync();
                record.TokenHash.Should().NotBe(token.GetProperty("access_token").GetString());
            }
            barrier.Reset(2);
            results = await Task.WhenAll(Exchange(true, token.GetProperty("refresh_token").GetString()!), Exchange(true, token.GetProperty("refresh_token").GetString()!));
            results.Count(x => x.HasValue).Should().Be(1);
            var rotated = results.Single(x => x.HasValue)!.Value;
            // Pause a refresh after it reads the active connection, revoke from another context, then resume.
            var pause = new SaveBarrier(2);
            var pausedOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).AddInterceptors(pause).Options;
            await using var pendingDb = new AppDbContext(pausedOptions);
            var pending = Service(pendingDb).ExchangeAsync("refresh_token", client, settings.Value.Resource, null, null, null,
                rotated.GetProperty("refresh_token").GetString(), null, default);
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using (var db = new AppDbContext(options))
            {
                var id = await db.McpAccessTokens.Select(x => x.Id).SingleAsync();
                (await new McpAccessTokenService(db, TimeProvider.System).RevokeAsync("alice", id)).Should().BeTrue();
            }
            pause.Release();
            await Assert.ThrowsAsync<OAuthException>(() => pending);
            await using (var db = new AppDbContext(options))
            {
                (await new McpAccessTokenService(db, TimeProvider.System).AuthenticateAsync(rotated.GetProperty("access_token").GetString()!)).Should().BeNull();
                await db.Users.Where(x => x.Id == "alice").ExecuteDeleteAsync();
                (await db.McpAccessTokens.AnyAsync()).Should().BeFalse();
                (await db.McpOAuthRefreshTokens.AnyAsync()).Should().BeFalse();
                (await db.McpOAuthRequests.AnyAsync()).Should().BeFalse();
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class SaveBarrier(int remaining) : SaveChangesInterceptor
    {
        private int count = remaining;
        private TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Reset(int value) { count = value; ready = new(TaskCreationOptions.RunContinuationsAsynchronously); Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public void Release() => ready.TrySetResult();
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            if (Interlocked.Decrement(ref count) == 0) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            return result;
        }
    }
    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES"))) Skip = "Requires disposable PostgreSQL."; }
    }
}

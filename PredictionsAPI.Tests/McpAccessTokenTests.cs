using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Auth;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class McpAccessTokenTests
{
    private readonly TestClock clock = new();
    private readonly string databaseName = Guid.NewGuid().ToString();

    private async Task<AppDbContext> SeedAsync(bool admin = false)
    {
        var db = DbContextFactory.Create(databaseName);
        db.Users.AddRange(DbContextFactory.MakeUser("alice", "Alice"), DbContextFactory.MakeUser("bob", "Bob"));
        db.Roles.Add(new IdentityRole { Id = "admin", Name = "Admin", NormalizedName = "ADMIN" });
        if (admin) db.UserRoles.Add(new IdentityUserRole<string> { UserId = "alice", RoleId = "admin" });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Creation_StoresOnlyHash_ReturnsSecretOnce_AndListsOnlyOwnedMetadata()
    {
        await using var db = await SeedAsync();
        var service = new McpAccessTokenService(db, clock);
        var first = (await service.CreateAsync("alice", new() { Name = " Codex " }))!;
        var second = (await service.CreateAsync("alice", new() { Name = "Hermes" }))!;
        await service.CreateAsync("bob", new() { Name = "Private" });

        first.Token.Should().StartWith("pred_mcp_").And.NotBe(second.Token);
        WebEncoders.Base64UrlDecode(first.Token[9..]).Length.Should().Be(32);
        first.Connection.Name.Should().Be("Codex");
        first.Connection.ExpiresAt.Should().Be(clock.GetUtcNow().AddDays(30));
        first.Connection.Scopes.Should().Equal(McpScopes.AppRead);
        var entity = await db.McpAccessTokens.FindAsync(first.Connection.Id);
        entity!.TokenHash.Should().HaveLength(64).And.NotContain(first.Token);
        JsonSerializer.Serialize(entity).Should().NotContain(first.Token).And.NotContain(entity.TokenHash);
        first.ToString().Should().NotContain(first.Token);

        // A fresh request still sees metadata but cannot recover a credential.
        await using var freshDb = DbContextFactory.Create(databaseName);
        var listing = await new McpAccessTokenService(freshDb, clock).ListAsync("alice");
        listing!.Tokens.Select(t => t.Name).Should().BeEquivalentTo("Codex", "Hermes");
        listing.CanGrantAdminScopes.Should().BeFalse();
        JsonSerializer.Serialize(listing).Should().NotContain(first.Token).And.NotContain("TokenHash");
        (await service.ListAsync("missing")).Should().BeNull();
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(90)]
    public async Task TokenExpiresAtExactBoundary(int days)
    {
        await using var db = await SeedAsync();
        var service = new McpAccessTokenService(db, clock);
        var token = (await service.CreateAsync("alice", new() { Name = "Codex", ExpirationDays = days }))!;
        clock.Now = token.Connection.ExpiresAt.AddTicks(-1);
        var principal = await service.AuthenticateAsync(token.Token);
        principal.Should().NotBeNull();
        clock.Now = token.Connection.ExpiresAt;
        (await service.AuthenticateAsync(token.Token)).Should().BeNull();
        (await service.HasPermissionAsync(principal!, McpScopes.AppRead)).Should().BeFalse();
    }

    [Fact]
    public async Task InvalidRequestsCannotCreateCredentials()
    {
        await using var db = await SeedAsync();
        var service = new McpAccessTokenService(db, clock);
        CreateMcpAccessTokenRequest[] invalid = [
            new() { Name = " " }, new() { Name = new string('x', 101) },
            new() { Name = "Codex", ExpirationDays = 8 }, new() { Name = "Codex", ExpirationDays = 0 },
            new() { Name = "Codex", Scopes = [] }, new() { Name = "Codex", Scopes = null! },
            new() { Name = "Codex", Scopes = ["*", "app:read"] },
            new() { Name = "Codex", Scopes = [null!] }
        ];
        foreach (var request in invalid)
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("alice", request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync("alice", new() { Name = "Codex", Scopes = [McpScopes.AdminRead] }));
        (await service.CreateAsync("missing", new() { Name = "Codex" })).Should().BeNull();
        db.McpAccessTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RevocationIsOwnedIdempotentAndInvalidatesRetainedPrincipal()
    {
        await using var db = await SeedAsync();
        var service = new McpAccessTokenService(db, clock);
        var first = (await service.CreateAsync("alice", new() { Name = "Codex" }))!;
        var second = (await service.CreateAsync("alice", new() { Name = "Hermes" }))!;
        var principal = (await service.AuthenticateAsync(first.Token))!;
        (await service.RevokeAsync("bob", first.Connection.Id)).Should().BeFalse();
        (await service.RevokeAsync("alice", Guid.NewGuid())).Should().BeFalse();
        (await service.HasPermissionAsync(principal, McpScopes.AppRead)).Should().BeTrue();
        (await service.RevokeAsync("alice", first.Connection.Id)).Should().BeTrue();
        (await service.RevokeAsync("alice", first.Connection.Id)).Should().BeTrue();
        (await service.HasPermissionAsync(principal, McpScopes.AppRead)).Should().BeFalse();
        (await service.AuthenticateAsync(first.Token)).Should().BeNull();
        (await service.AuthenticateAsync(second.Token)).Should().NotBeNull();
    }

    [Fact]
    public async Task AdminScopesRequireLiveRole_AndPromotionDoesNotExpandScopes()
    {
        await using var db = await SeedAsync(admin: true);
        var service = new McpAccessTokenService(db, clock);
        var token = (await service.CreateAsync("alice", new() { Name = "Admin", Scopes = [McpScopes.AdminRead, McpScopes.AppRead] }))!;
        var principal = (await service.AuthenticateAsync(token.Token))!;
        (await service.HasPermissionAsync(principal, McpScopes.AdminRead)).Should().BeTrue();
        (await service.HasPermissionAsync(principal, McpScopes.AdminWrite)).Should().BeFalse();
        db.UserRoles.Remove(await db.UserRoles.SingleAsync());
        await db.SaveChangesAsync();
        (await service.HasPermissionAsync(principal, McpScopes.AdminRead)).Should().BeFalse();
        (await service.HasPermissionAsync(principal, McpScopes.AppRead)).Should().BeTrue();
        (await service.ListAsync("alice"))!.CanGrantAdminScopes.Should().BeFalse();

        var limited = (await service.CreateAsync("bob", new() { Name = "User" }))!;
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "bob", RoleId = "admin" });
        await db.SaveChangesAsync();
        var promoted = (await service.AuthenticateAsync(limited.Token))!;
        (await service.HasPermissionAsync(promoted, McpScopes.AdminRead)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletedOwnersAndForgedOrForeignPrincipalsAreDenied()
    {
        await using var db = await SeedAsync();
        var service = new McpAccessTokenService(db, clock);
        var token = (await service.CreateAsync("alice", new() { Name = "Codex" }))!;
        var principal = (await service.AuthenticateAsync(token.Token))!;
        foreach (var value in new[] { "", "invalid", "pred_mcp_" + new string('x', 43), token.Token + "x" })
            (await service.AuthenticateAsync(value)).Should().BeNull();
        var wrongScheme = new ClaimsPrincipal(new ClaimsIdentity(principal.Claims, "Bearer"));
        (await service.HasPermissionAsync(wrongScheme, McpScopes.AppRead)).Should().BeFalse();
        var wrongOwner = new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, "bob"), new(McpAuthentication.TokenIdClaim, token.Connection.Id.ToString())
        ], McpAuthentication.Scheme));
        (await service.HasPermissionAsync(wrongOwner, McpScopes.AppRead)).Should().BeFalse();
        (await service.HasPermissionAsync(principal, "unknown")).Should().BeFalse();
        db.Users.Remove((await db.Users.FindAsync("alice"))!);
        await db.SaveChangesAsync();
        (await service.AuthenticateAsync(token.Token)).Should().BeNull();
        (await service.HasPermissionAsync(principal, McpScopes.AppRead)).Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticationRecordsLastUseAndKeepsAccountsSeparate()
    {
        await using var db = await SeedAsync();
        var service = new McpAccessTokenService(db, clock);
        var alice = (await service.CreateAsync("alice", new() { Name = "Codex" }))!;
        var bob = (await service.CreateAsync("bob", new() { Name = "Codex", Scopes = [McpScopes.PredictionsWrite] }))!;
        clock.Now = clock.Now.AddMinutes(2);
        var a = (await service.AuthenticateAsync(alice.Token))!;
        var b = (await service.AuthenticateAsync(bob.Token))!;
        a.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("alice");
        b.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("bob");
        (await service.HasPermissionAsync(a, McpScopes.PredictionsWrite)).Should().BeFalse();
        (await service.HasPermissionAsync(b, McpScopes.AppRead)).Should().BeFalse();
        (await service.ListAsync("alice"))!.Tokens.Single().LastUsedAt.Should().Be(clock.Now);
    }

    public class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

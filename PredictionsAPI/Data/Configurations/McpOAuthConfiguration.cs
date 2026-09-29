using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PredictionsAPI.Entities;

namespace PredictionsAPI.Data.Configurations;

public class McpOAuthClientConfiguration : IEntityTypeConfiguration<McpOAuthClient>
{
    public void Configure(EntityTypeBuilder<McpOAuthClient> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(2048);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.RedirectUris).IsRequired();
        b.Property(x => x.AuthMethod).HasMaxLength(32);
        b.Property(x => x.SecretHash).HasMaxLength(64);
    }
}
public class McpOAuthRequestConfiguration : IEntityTypeConfiguration<McpOAuthRequest>
{
    public void Configure(EntityTypeBuilder<McpOAuthRequest> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.ClientId).HasMaxLength(2048);
        b.Property(x => x.ClientName).HasMaxLength(100);
        b.Property(x => x.RedirectUri).HasMaxLength(2048);
        b.Property(x => x.Resource).HasMaxLength(2048);
        b.Property(x => x.State).HasMaxLength(2048);
        b.Property(x => x.Challenge).HasMaxLength(43);
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.HasIndex(x => x.CodeHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class McpOAuthRefreshTokenConfiguration : IEntityTypeConfiguration<McpOAuthRefreshToken>
{
    public void Configure(EntityTypeBuilder<McpOAuthRefreshToken> b)
    {
        b.HasKey(x => x.Hash);
        b.Property(x => x.Hash).HasMaxLength(64);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne(x => x.Connection).WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

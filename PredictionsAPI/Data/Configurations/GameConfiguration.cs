using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PredictionsAPI.Entities;

namespace PredictionsAPI.Data.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.HomeTeam)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.AwayTeam)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasMany(g => g.Predictions)
            .WithOne(p => p.Game)
            .HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

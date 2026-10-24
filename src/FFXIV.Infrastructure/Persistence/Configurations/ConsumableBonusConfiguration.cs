using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class ConsumableBonusConfiguration : IEntityTypeConfiguration<ConsumableBonus>
{
    public void Configure(EntityTypeBuilder<ConsumableBonus> builder)
    {
        builder.ToTable("ConsumableBonuses");

        builder.HasKey(c => new { c.ItemId, c.Stat });

        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.Stat).HasConversion<string>().HasMaxLength(16);

        builder.HasOne(c => c.Item)
            .WithMany()
            .HasForeignKey(c => c.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

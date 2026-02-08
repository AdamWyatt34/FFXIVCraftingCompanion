using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class GatheringInfoConfiguration : IEntityTypeConfiguration<GatheringInfo>
{
    public void Configure(EntityTypeBuilder<GatheringInfo> builder)
    {
        builder.ToTable("GatheringInfos");

        builder.HasKey(g => g.GatheringInfoId);
        builder.Property(g => g.GatheringInfoId).ValueGeneratedOnAdd();

        builder.HasOne(g => g.Item)
            .WithMany(i => i.GatheringInfos)
            .HasForeignKey(g => g.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.ItemId);

        builder.Property(g => g.GatheringType).HasConversion<string>();
        builder.Property(g => g.ClassJob).HasMaxLength(8);
        builder.Property(g => g.ZoneName).HasMaxLength(128);
        builder.Property(g => g.AreaName).HasMaxLength(128);
        builder.Property(g => g.SpawnTimes).HasColumnType("text");
    }
}

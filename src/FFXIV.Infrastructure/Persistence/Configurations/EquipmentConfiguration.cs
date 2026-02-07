using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");

        builder.HasKey(e => e.EquipmentId);
        builder.Property(e => e.EquipmentId).ValueGeneratedNever();

        builder.HasIndex(e => new { e.ClassJobCategory, e.EquipLevel });
        builder.HasIndex(e => new { e.ClassJobCategory, e.ItemLevel });

        builder.Property(e => e.EquipSlot).HasConversion<string>();
    }
}

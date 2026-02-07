using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");

        builder.HasKey(i => i.ItemId);
        builder.Property(i => i.ItemId).ValueGeneratedNever();

        builder.Property(i => i.Name).HasMaxLength(256);
        builder.HasIndex(i => i.Name);

        builder.Property(i => i.Category).HasConversion<string>();
    }
}

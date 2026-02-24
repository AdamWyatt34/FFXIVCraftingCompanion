using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class ItemSourceConfiguration : IEntityTypeConfiguration<ItemSource>
{
    public void Configure(EntityTypeBuilder<ItemSource> builder)
    {
        builder.ToTable("ItemSources");

        builder.HasKey(s => s.ItemSourceId);
        builder.Property(s => s.ItemSourceId).ValueGeneratedOnAdd();

        builder.HasOne(s => s.Item)
            .WithMany(i => i.Sources)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.ItemId);
        builder.HasIndex(s => new { s.ItemId, s.SourceType }).IsUnique();

        builder.Property(s => s.SourceType).HasConversion<string>();
        builder.Property(s => s.SourceDetails);
    }
}

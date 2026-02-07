using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class SyncMetadataConfiguration : IEntityTypeConfiguration<SyncMetadata>
{
    public void Configure(EntityTypeBuilder<SyncMetadata> builder)
    {
        builder.ToTable("SyncMetadata");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        builder.HasIndex(s => s.EntityType).IsUnique();
    }
}

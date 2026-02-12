using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class RecipeLevelEntryConfiguration : IEntityTypeConfiguration<RecipeLevelEntry>
{
    public void Configure(EntityTypeBuilder<RecipeLevelEntry> builder)
    {
        builder.ToTable("RecipeLevelEntries");
        builder.HasKey(r => r.RecipeLevel);
        builder.Property(r => r.RecipeLevel).ValueGeneratedNever();
    }
}

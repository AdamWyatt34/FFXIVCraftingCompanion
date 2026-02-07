using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes");

        builder.HasKey(r => r.RecipeId);
        builder.Property(r => r.RecipeId).ValueGeneratedNever();

        builder.HasOne(r => r.ResultItem)
            .WithMany()
            .HasForeignKey(r => r.ResultItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Ingredients)
            .WithOne(ri => ri.Recipe)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ResultItemId);

        builder.Property(r => r.CraftType).HasConversion<string>();
    }
}

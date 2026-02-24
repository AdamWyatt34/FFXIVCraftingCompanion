using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients");

        builder.HasKey(ri => new { ri.RecipeId, ri.ItemId });

        builder.HasIndex(ri => ri.ItemId);

        builder.HasOne(ri => ri.Item)
            .WithMany()
            .HasForeignKey(ri => ri.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

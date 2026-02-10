using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class FavoriteRecipeConfiguration : IEntityTypeConfiguration<FavoriteRecipe>
{
    public void Configure(EntityTypeBuilder<FavoriteRecipe> builder)
    {
        builder.ToTable("FavoriteRecipes");

        builder.HasKey(fr => fr.Id);
        builder.Property(fr => fr.Id).UseIdentityByDefaultColumn();

        builder.Property(fr => fr.UserId).HasMaxLength(450);
        builder.Property(fr => fr.Note).HasMaxLength(1024);

        builder.HasIndex(fr => new { fr.UserId, fr.RecipeId }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(fr => fr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fr => fr.Recipe)
            .WithMany()
            .HasForeignKey(fr => fr.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

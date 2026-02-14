using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class SharedRotationConfiguration : IEntityTypeConfiguration<SharedRotation>
{
    public void Configure(EntityTypeBuilder<SharedRotation> builder)
    {
        builder.ToTable("SharedRotations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();

        builder.Property(r => r.UserId).HasMaxLength(450);
        builder.Property(r => r.Name).HasMaxLength(256);
        builder.Property(r => r.Notes).HasMaxLength(2048);
        builder.Property(r => r.ActionIds).HasColumnType("jsonb");
        builder.Property(r => r.Source).HasConversion<string>();
        builder.Property(r => r.Algorithm).HasMaxLength(64);
        builder.Property(r => r.QualityPercent).HasPrecision(5, 2);

        builder.HasIndex(r => r.RecipeId);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.RecipeId, r.QualityPercent });
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => r.Upvotes);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Recipe)
            .WithMany()
            .HasForeignKey(r => r.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

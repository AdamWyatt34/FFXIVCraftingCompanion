using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id).ValueGeneratedOnAdd();

        builder.Property(rt => rt.Token).HasMaxLength(512);
        builder.HasIndex(rt => rt.Token).IsUnique();

        builder.Property(rt => rt.UserId).HasMaxLength(450);
        builder.HasIndex(rt => rt.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

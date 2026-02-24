using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(128);
        builder.Property(u => u.AvatarUrl).HasMaxLength(512);
        builder.Property(u => u.DiscordId).HasMaxLength(64);

        builder.HasIndex(u => u.DiscordId)
            .IsUnique()
            .HasFilter("[DiscordId] IS NOT NULL");
    }
}

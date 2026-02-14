using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class RotationVoteConfiguration : IEntityTypeConfiguration<RotationVote>
{
    public void Configure(EntityTypeBuilder<RotationVote> builder)
    {
        builder.ToTable("RotationVotes");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).UseIdentityByDefaultColumn();

        builder.Property(v => v.UserId).HasMaxLength(450);

        builder.HasIndex(v => new { v.UserId, v.RotationId }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Rotation)
            .WithMany()
            .HasForeignKey(v => v.RotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

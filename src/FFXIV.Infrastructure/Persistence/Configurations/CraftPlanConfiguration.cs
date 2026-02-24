using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class CraftPlanConfiguration : IEntityTypeConfiguration<CraftPlan>
{
    public void Configure(EntityTypeBuilder<CraftPlan> builder)
    {
        builder.ToTable("CraftPlans");

        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id).HasMaxLength(36);

        builder.Property(cp => cp.UserId).HasMaxLength(450);
        builder.Property(cp => cp.Name).HasMaxLength(256);
        builder.Property(cp => cp.Items);

        builder.HasIndex(cp => cp.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(cp => cp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

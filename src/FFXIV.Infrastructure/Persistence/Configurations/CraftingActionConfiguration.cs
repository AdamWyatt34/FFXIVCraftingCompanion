using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class CraftingActionConfiguration : IEntityTypeConfiguration<CraftingAction>
{
    public void Configure(EntityTypeBuilder<CraftingAction> builder)
    {
        builder.ToTable("CraftingActions");

        builder.HasKey(a => a.ActionId);
        builder.Property(a => a.ActionId).ValueGeneratedNever();

        builder.Property(a => a.Name).HasMaxLength(128);
    }
}

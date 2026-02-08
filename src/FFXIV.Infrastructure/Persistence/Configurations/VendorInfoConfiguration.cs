using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class VendorInfoConfiguration : IEntityTypeConfiguration<VendorInfo>
{
    public void Configure(EntityTypeBuilder<VendorInfo> builder)
    {
        builder.ToTable("VendorInfos");

        builder.HasKey(v => v.VendorInfoId);
        builder.Property(v => v.VendorInfoId).ValueGeneratedOnAdd();

        builder.HasOne(v => v.Item)
            .WithMany(i => i.VendorInfos)
            .HasForeignKey(v => v.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.ItemId);

        builder.Property(v => v.NpcName).HasMaxLength(128);
        builder.Property(v => v.ZoneName).HasMaxLength(128);
        builder.Property(v => v.CurrencyType).HasMaxLength(64);

        builder.HasOne(v => v.CurrencyItem)
            .WithMany()
            .HasForeignKey(v => v.CurrencyItemId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}

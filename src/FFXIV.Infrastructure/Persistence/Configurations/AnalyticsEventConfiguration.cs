using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.ToTable("AnalyticsEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Event)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.Metadata)
            .HasMaxLength(4096);

        builder.Property(e => e.PageUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.Referrer)
            .HasMaxLength(2048);

        builder.HasIndex(e => e.Event);
        builder.HasIndex(e => e.CreatedAt);
    }
}

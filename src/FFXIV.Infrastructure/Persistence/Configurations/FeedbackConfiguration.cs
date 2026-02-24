using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedbacks");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Message)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(e => e.Email)
            .HasMaxLength(320);

        builder.Property(e => e.PageUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(512);

        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.CreatedAt);
    }
}

using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class SavedListConfiguration : IEntityTypeConfiguration<SavedList>
{
    public void Configure(EntityTypeBuilder<SavedList> builder)
    {
        builder.ToTable("SavedLists");

        builder.HasKey(sl => sl.Id);
        builder.Property(sl => sl.Id).ValueGeneratedOnAdd();

        builder.Property(sl => sl.UserId).HasMaxLength(450);
        builder.Property(sl => sl.Name).HasMaxLength(256);
        builder.Property(sl => sl.Data);
        builder.Property(sl => sl.Type).HasConversion<string>();

        builder.HasIndex(sl => new { sl.UserId, sl.Type });
        builder.HasIndex(sl => new { sl.UserId, sl.IsPinned });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(sl => sl.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

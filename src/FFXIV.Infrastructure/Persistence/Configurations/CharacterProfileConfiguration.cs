using System.Text.Json;
using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FFXIV.Infrastructure.Persistence.Configurations;

public class CharacterProfileConfiguration : IEntityTypeConfiguration<CharacterProfile>
{
    public void Configure(EntityTypeBuilder<CharacterProfile> builder)
    {
        builder.ToTable("CharacterProfiles");

        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id).ValueGeneratedOnAdd();

        builder.Property(cp => cp.UserId).HasMaxLength(450);
        builder.HasIndex(cp => cp.UserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CharacterProfile>(cp => cp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(cp => cp.CharacterName).HasMaxLength(128);
        builder.Property(cp => cp.ServerName).HasMaxLength(64);
        builder.Property(cp => cp.DataCenter).HasMaxLength(64);
        builder.Property(cp => cp.ActiveJob).HasMaxLength(8);
        builder.Property(cp => cp.AvatarUrl).HasMaxLength(512);

        builder.Property(cp => cp.LodestoneId).HasMaxLength(64);
        builder.HasIndex(cp => cp.LodestoneId)
            .IsUnique()
            .HasFilter("[LodestoneId] IS NOT NULL");

        builder.Property(cp => cp.Source).HasConversion<string>();

        var dictComparer = new ValueComparer<Dictionary<string, int>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) ==
                       JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, int>>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        var nullableDictComparer = new ValueComparer<Dictionary<string, int>?>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) ==
                       JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null));

        builder.Property(cp => cp.JobLevels)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(dictComparer);

        builder.Property(cp => cp.CurrentGear)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(nullableDictComparer);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class ChantConfigConfiguration : IEntityTypeConfiguration<ChantConfig>
{
    public void Configure(EntityTypeBuilder<ChantConfig> builder)
    {
        builder.ToTable("ChantConfigs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(1000);

        // Comma-separated deity ids — same convention as Festivals.Regions / Deities.Days.
        builder.Property(c => c.DeityIds).HasMaxLength(2000);

        builder.Property(c => c.ChantText).IsRequired();

        builder.Property(c => c.AudioFileName).HasMaxLength(255);
        builder.Property(c => c.AudioContentType).HasMaxLength(100);

        builder.Property(c => c.TimeDescription).HasMaxLength(200);

        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("UX_ChantConfigs_Name");
        builder.HasIndex(c => c.ChantId).HasDatabaseName("IX_ChantConfigs_ChantId");

        // A category cannot be deleted while chants are configured under it.
        builder.HasOne(c => c.Chant)
            .WithMany()
            .HasForeignKey(c => c.ChantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChantConfigAudioConfiguration : IEntityTypeConfiguration<ChantConfigAudio>
{
    public void Configure(EntityTypeBuilder<ChantConfigAudio> builder)
    {
        builder.ToTable("ChantConfigAudios");
        builder.HasKey(a => a.ChantConfigId);
        builder.Property(a => a.ChantConfigId).ValueGeneratedNever();

        builder.Property(a => a.Data).IsRequired();

        builder.Property(a => a.CreatedBy).HasMaxLength(100);
        builder.Property(a => a.ModifiedBy).HasMaxLength(100);

        // Deleting the chant config takes its audio with it.
        builder.HasOne(a => a.ChantConfig)
            .WithOne(c => c.Audio)
            .HasForeignKey<ChantConfigAudio>(a => a.ChantConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

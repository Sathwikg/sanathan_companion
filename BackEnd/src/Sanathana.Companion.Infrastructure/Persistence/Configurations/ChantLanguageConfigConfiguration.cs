using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class ChantLanguageConfigConfiguration : IEntityTypeConfiguration<ChantLanguageConfig>
{
    public void Configure(EntityTypeBuilder<ChantLanguageConfig> builder)
    {
        builder.ToTable("ChantLanguageConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One text per language per chant.
        builder.HasIndex(x => new { x.ChantConfigId, x.LanguageId })
            .IsUnique().HasDatabaseName("UX_ChantLanguageConfigs_Chant_Language");

        // Deleting the chant removes its translations; a language cannot be deleted while used.
        builder.HasOne(x => x.ChantConfig)
            .WithMany()
            .HasForeignKey(x => x.ChantConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

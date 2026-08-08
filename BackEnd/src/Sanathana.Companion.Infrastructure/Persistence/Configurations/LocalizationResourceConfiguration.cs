using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class LocalizationResourceConfiguration : IEntityTypeConfiguration<LocalizationResource>
{
    public void Configure(EntityTypeBuilder<LocalizationResource> builder)
    {
        builder.ToTable("LocalizationResources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Namespace).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One value per key per language.
        builder.HasIndex(x => new { x.LanguageId, x.Key })
            .IsUnique().HasDatabaseName("UX_LocalizationResources_Language_Key");

        // Bundle building filters by language then groups by namespace.
        builder.HasIndex(x => new { x.LanguageId, x.Namespace })
            .HasDatabaseName("IX_LocalizationResources_Language_Namespace");

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

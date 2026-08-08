using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class EntityTranslationConfiguration : IEntityTypeConfiguration<EntityTranslation>
{
    public void Configure(EntityTypeBuilder<EntityTranslation> builder)
    {
        builder.ToTable("EntityTranslations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(80);
        builder.Property(x => x.EntityKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Field).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One translation per field per row per language.
        builder.HasIndex(x => new { x.LanguageId, x.EntityType, x.EntityKey, x.Field })
            .IsUnique().HasDatabaseName("UX_EntityTranslations_Lang_Type_Key_Field");

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

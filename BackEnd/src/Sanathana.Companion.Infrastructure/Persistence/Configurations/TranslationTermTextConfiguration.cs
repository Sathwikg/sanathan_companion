using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class TranslationTermTextConfiguration : IEntityTypeConfiguration<TranslationTermText>
{
    public void Configure(EntityTypeBuilder<TranslationTermText> builder)
    {
        builder.ToTable("TranslationTermTexts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Text).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One rendering per term per language.
        builder.HasIndex(x => new { x.LanguageId, x.TermId })
            .IsUnique().HasDatabaseName("UX_TranslationTermTexts_Lang_Term");

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

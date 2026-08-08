using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class TranslationTermConfiguration : IEntityTypeConfiguration<TranslationTerm>
{
    public void Configure(EntityTypeBuilder<TranslationTerm> builder)
    {
        builder.ToTable("TranslationTerms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TermKey).IsRequired().HasMaxLength(400);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(400);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(60);
        // Stored as text so the table stays readable in psql, matching the rest of the schema.
        builder.Property(x => x.Origin).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // The vocabulary is keyed by the text itself, independent of language.
        builder.HasIndex(x => x.TermKey).IsUnique().HasDatabaseName("UX_TranslationTerms_Key");
        builder.HasIndex(x => new { x.Category, x.IsActive }).HasDatabaseName("IX_TranslationTerms_Category_Active");

        builder.HasMany(x => x.Texts)
            .WithOne(x => x.Term!)
            .HasForeignKey(x => x.TermId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

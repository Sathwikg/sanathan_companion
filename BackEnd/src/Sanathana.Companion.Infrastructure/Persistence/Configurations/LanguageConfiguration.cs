using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("Languages");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Name).IsRequired().HasMaxLength(150);
        builder.Property(l => l.NativeName).HasMaxLength(150);
        builder.Property(l => l.Code).HasMaxLength(10);
        builder.Property(l => l.Description).HasMaxLength(500);

        // Comma-separated region ids — same convention as Festivals.Regions.
        builder.Property(l => l.Regions).HasMaxLength(2000);

        builder.Property(l => l.CreatedBy).HasMaxLength(100);
        builder.Property(l => l.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(l => l.Name).IsUnique().HasDatabaseName("UX_Languages_Name");
    }
}

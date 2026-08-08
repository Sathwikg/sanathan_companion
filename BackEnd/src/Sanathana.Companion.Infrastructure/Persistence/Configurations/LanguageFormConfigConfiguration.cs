using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class LanguageFormConfigConfiguration : IEntityTypeConfiguration<LanguageFormConfig>
{
    public void Configure(EntityTypeBuilder<LanguageFormConfig> builder)
    {
        builder.ToTable("LanguageFormConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One row per form per language.
        builder.HasIndex(x => new { x.LanguageId, x.MenuModuleId })
            .IsUnique().HasDatabaseName("UX_LanguageFormConfigs_Language_Module");

        // Deleting a form drops its language rows; a language in use is protected by its own cascade.
        builder.HasOne(x => x.MenuModule)
            .WithMany()
            .HasForeignKey(x => x.MenuModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class PujaMaterialConfiguration : IEntityTypeConfiguration<PujaMaterial>
{
    public void Configure(EntityTypeBuilder<PujaMaterial> builder)
    {
        builder.ToTable("PujaMaterials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Quantity).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.PujaId, x.DisplayOrder }).HasDatabaseName("IX_PujaMaterials_Puja_Order");

        // Materials belong to the puja and have no life without it.
        builder.HasOne(x => x.Puja).WithMany()
            .HasForeignKey(x => x.PujaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PujaStepConfiguration : IEntityTypeConfiguration<PujaStep>
{
    public void Configure(EntityTypeBuilder<PujaStep> builder)
    {
        builder.ToTable("PujaSteps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Step numbers are contiguous per puja and the list always reads in order.
        builder.HasIndex(x => new { x.PujaId, x.StepNumber })
            .IsUnique().HasDatabaseName("UX_PujaSteps_Puja_Number");

        builder.HasOne(x => x.Puja).WithMany()
            .HasForeignKey(x => x.PujaId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Texts).WithOne(t => t.PujaStep!)
            .HasForeignKey(t => t.PujaStepId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PujaStepTextConfiguration : IEntityTypeConfiguration<PujaStepText>
{
    public void Configure(EntityTypeBuilder<PujaStepText> builder)
    {
        builder.ToTable("PujaStepTexts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One text per language per step.
        builder.HasIndex(x => new { x.PujaStepId, x.LanguageId })
            .IsUnique().HasDatabaseName("UX_PujaStepTexts_Step_Language");

        // A language still in use must not be deletable out from under the content.
        builder.HasOne(x => x.Language).WithMany()
            .HasForeignKey(x => x.LanguageId).OnDelete(DeleteBehavior.Restrict);
    }
}

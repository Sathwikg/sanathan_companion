using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class PujaConfiguration : IEntityTypeConfiguration<Puja>
{
    public void Configure(EntityTypeBuilder<Puja> builder)
    {
        builder.ToTable("Pujas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Unique per festival rather than globally — the same rite may appear under two festivals.
        builder.HasIndex(x => new { x.FestivalId, x.Name })
            .IsUnique().HasDatabaseName("UX_Pujas_Festival_Name");

        // Restrict, not Cascade: deleting a festival must not silently take its pujas with it.
        builder.HasOne(x => x.Festival)
            .WithMany()
            .HasForeignKey(x => x.FestivalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

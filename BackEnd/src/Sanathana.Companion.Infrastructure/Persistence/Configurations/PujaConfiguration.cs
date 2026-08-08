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
        //
        // AreNullsDistinct(false) is load-bearing now that the festival is optional. By default
        // Postgres treats every NULL as distinct, so without it two unmapped pujas could share a
        // name and the constraint would quietly do nothing for exactly the rows most likely to
        // collide. Requires PG15+; the deployment is on 17.
        builder.HasIndex(x => new { x.FestivalId, x.Name })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("UX_Pujas_Festival_Name");

        builder.HasIndex(x => x.DeityId).HasDatabaseName("IX_Pujas_Deity");

        // Both links are optional, but Restrict on each: removing a festival or a deity must not
        // silently delete the pujas that reference it, nor quietly blank the mapping.
        builder.HasOne(x => x.Festival)
            .WithMany()
            .HasForeignKey(x => x.FestivalId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Deity)
            .WithMany()
            .HasForeignKey(x => x.DeityId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

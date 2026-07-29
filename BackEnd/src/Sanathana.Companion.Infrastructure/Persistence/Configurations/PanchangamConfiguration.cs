using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class PanchangamConfiguration : IEntityTypeConfiguration<Panchangam>
{
    public void Configure(EntityTypeBuilder<Panchangam> builder)
    {
        builder.ToTable("Panchangams");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.DayOfWeek).HasMaxLength(20);
        builder.Property(p => p.TeluguSamvatsaram).HasMaxLength(50);
        builder.Property(p => p.Ayanam).HasMaxLength(30);
        builder.Property(p => p.Masam).HasMaxLength(50);
        builder.Property(p => p.Paksham).HasMaxLength(30);
        builder.Property(p => p.Rutuvu).HasMaxLength(30);
        builder.Property(p => p.TithiDetails).HasMaxLength(200);
        builder.Property(p => p.NakshatramDetails).HasMaxLength(200);
        builder.Property(p => p.AmruthaKalam).HasMaxLength(120);
        builder.Property(p => p.AbhijitMuhurtham).HasMaxLength(120);
        builder.Property(p => p.Durmuhurtham).HasMaxLength(200);
        builder.Property(p => p.RahuKalam).HasMaxLength(120);
        builder.Property(p => p.Yamagandam).HasMaxLength(120);
        builder.Property(p => p.Varjyam).HasMaxLength(200);
        builder.Property(p => p.Gulika).HasMaxLength(120);

        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.ModifiedBy).HasMaxLength(100);

        // One row per date per region — the composite identity the requirement asks for.
        builder.HasIndex(p => new { p.Date, p.RegionId }).IsUnique().HasDatabaseName("UX_Panchangams_Date_Region");
        builder.HasIndex(p => new { p.Year, p.RegionId }).HasDatabaseName("IX_Panchangams_Year_Region");

        builder.HasOne(p => p.Region)
            .WithMany()
            .HasForeignKey(p => p.RegionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

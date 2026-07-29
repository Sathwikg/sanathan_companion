using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class FestivalConfiguration : IEntityTypeConfiguration<Festival>
{
    public void Configure(EntityTypeBuilder<Festival> builder)
    {
        builder.ToTable("Festivals");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Name).IsRequired().HasMaxLength(150);
        builder.Property(f => f.Description).HasMaxLength(500);
        builder.Property(f => f.Regions).HasMaxLength(2000);
        builder.Property(f => f.CreatedBy).HasMaxLength(100);
        builder.Property(f => f.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(f => new { f.Year, f.Name }).IsUnique().HasDatabaseName("UX_Festivals_Year_Name");
        builder.HasIndex(f => f.Year).HasDatabaseName("IX_Festivals_Year");

        builder.HasData(FestivalSeed.Data());
    }
}

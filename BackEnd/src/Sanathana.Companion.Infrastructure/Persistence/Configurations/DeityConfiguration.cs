using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class DeityConfiguration : IEntityTypeConfiguration<Deity>
{
    public void Configure(EntityTypeBuilder<Deity> builder)
    {
        builder.ToTable("Deities");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(150);
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.WelcomeNote).HasMaxLength(1000);
        builder.Property(d => d.DeityType).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Regions).HasMaxLength(1000);
        builder.Property(d => d.Festivals).HasMaxLength(1000);
        builder.Property(d => d.Days).HasMaxLength(200);
        builder.Property(d => d.ImageContentType).HasMaxLength(100);
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(d => d.Name).IsUnique().HasDatabaseName("UX_Deities_Name");
    }
}

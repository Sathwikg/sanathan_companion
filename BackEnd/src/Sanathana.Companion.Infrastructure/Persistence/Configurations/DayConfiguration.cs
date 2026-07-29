using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class DayConfiguration : IEntityTypeConfiguration<Day>
{
    public void Configure(EntityTypeBuilder<Day> builder)
    {
        builder.ToTable("Days");
        builder.HasKey(d => d.DayId);
        builder.Property(d => d.DayId).ValueGeneratedNever();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(20);
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.ModifiedBy).HasMaxLength(100);

        var names = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var seed = new Day[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            seed[i] = new Day
            {
                DayId = i + 1,
                Name = names[i],
                DisplayOrder = i + 1,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            };
        }
        builder.HasData(seed);
    }
}

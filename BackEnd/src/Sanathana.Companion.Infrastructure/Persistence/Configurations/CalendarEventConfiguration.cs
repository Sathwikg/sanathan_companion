using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("CalendarEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Title).IsRequired().HasMaxLength(CalendarEvent.MaxTitleLength);
        builder.Property(e => e.Notes).HasMaxLength(CalendarEvent.MaxNotesLength);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(CalendarEvent.MaxKindLength);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.ModifiedBy).HasMaxLength(100);

        // The month view asks "this seeker, these dates"; the repeats are found by the same index.
        builder.HasIndex(e => new { e.UserId, e.Date }).HasDatabaseName("IX_CalendarEvents_User_Date");
    }
}

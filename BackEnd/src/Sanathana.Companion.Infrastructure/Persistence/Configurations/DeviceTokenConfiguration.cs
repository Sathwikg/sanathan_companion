using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

/// <summary>
/// Only the two columns added with the notification engine are described here. The table itself
/// was created by convention in AddDeviceToken, and restating its older columns with lengths would
/// make the next migration alter every one of them for no gain.
/// </summary>
public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.Property(d => d.TimeZoneId).HasMaxLength(64);
        builder.Property(d => d.Language).HasMaxLength(10);
    }
}

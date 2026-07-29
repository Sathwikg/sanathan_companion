using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class NotificationConfigConfiguration : IEntityTypeConfiguration<NotificationConfig>
{
    public void Configure(EntityTypeBuilder<NotificationConfig> builder)
    {
        builder.ToTable("NotificationConfigs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(150);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.ModifiedBy).HasMaxLength(100);

        // One notification config per module.
        builder.HasIndex(c => c.MenuModuleId).IsUnique().HasDatabaseName("UX_NotificationConfigs_Module");

        // Removing a form removes its notification config (and, in turn, user preferences).
        builder.HasOne(c => c.MenuModule)
            .WithMany()
            .HasForeignKey(c => c.MenuModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserNotificationSettingConfiguration : IEntityTypeConfiguration<UserNotificationSetting>
{
    public void Configure(EntityTypeBuilder<UserNotificationSetting> builder)
    {
        builder.ToTable("UserNotificationSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.CreatedBy).HasMaxLength(100);
        builder.Property(s => s.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(s => s.UserId).IsUnique().HasDatabaseName("UX_UserNotificationSettings_User");
    }
}

public class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(p => new { p.UserId, p.NotificationConfigId })
            .IsUnique().HasDatabaseName("UX_UserNotificationPreferences_User_Config");

        builder.HasOne(p => p.NotificationConfig)
            .WithMany()
            .HasForeignKey(p => p.NotificationConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

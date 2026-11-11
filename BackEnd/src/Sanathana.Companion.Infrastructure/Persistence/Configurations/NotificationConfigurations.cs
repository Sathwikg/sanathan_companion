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

        builder.Property(c => c.Kind).HasMaxLength(40).IsRequired().HasDefaultValue(NotificationConfig.ReminderKind);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);

        // One config per notification type per form. Until types existed this was one per form.
        builder.HasIndex(c => new { c.MenuModuleId, c.Kind }).IsUnique().HasDatabaseName("UX_NotificationConfigs_Module_Kind");

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

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Kind).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Title).HasMaxLength(NotificationLog.MaxTitleLength).IsRequired();
        builder.Property(l => l.Body).HasMaxLength(NotificationLog.MaxBodyLength).IsRequired();
        builder.Property(l => l.Route).HasMaxLength(200);
        builder.Property(l => l.DedupeKey).HasMaxLength(NotificationLog.MaxDedupeKeyLength).IsRequired();
        builder.Property(l => l.Status).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Trigger).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Error).HasMaxLength(1000);
        builder.Property(l => l.CreatedBy).HasMaxLength(100);
        builder.Property(l => l.ModifiedBy).HasMaxLength(100);

        // The guarantee that a notification is sent once: the second writer of a key is refused.
        builder.HasIndex(l => l.DedupeKey).IsUnique().HasDatabaseName("UX_NotificationLogs_DedupeKey");

        // The seeker's inbox, newest first.
        builder.HasIndex(l => new { l.UserId, l.CreatedDate }).HasDatabaseName("IX_NotificationLogs_User_Created");

        // The console's history and its per-day counts.
        builder.HasIndex(l => l.CreatedDate).HasDatabaseName("IX_NotificationLogs_Created");
    }
}

public class NotificationRunConfiguration : IEntityTypeConfiguration<NotificationRun>
{
    public void Configure(EntityTypeBuilder<NotificationRun> builder)
    {
        builder.ToTable("NotificationRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Kind).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Trigger).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Error).HasMaxLength(1000);
        builder.Property(r => r.CreatedBy).HasMaxLength(100);
        builder.Property(r => r.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(r => new { r.Kind, r.StartedAtUtc }).HasDatabaseName("IX_NotificationRuns_Kind_Started");
    }
}

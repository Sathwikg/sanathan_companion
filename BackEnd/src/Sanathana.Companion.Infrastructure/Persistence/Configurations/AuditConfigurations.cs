using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class AuditSettingsConfiguration : IEntityTypeConfiguration<AuditSettings>
{
    public void Configure(EntityTypeBuilder<AuditSettings> builder)
    {
        builder.ToTable("AuditSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.CreatedBy).HasMaxLength(100);
        builder.Property(s => s.ModifiedBy).HasMaxLength(100);

        // Seeded active with default retention
        builder.HasData(new AuditSettings
        {
            Id = SeedConstants.AuditSettingsId,
            IsGlobalAuditEnabled = true,
            TrackUserSessions = true,
            TrackPageNavigation = true,
            TrackDataModifications = true,
            TrackErrorLogs = true,
            AuditRetentionDays = 90,
            ErrorRetentionDays = 30,
            CreatedBy = "system",
            CreatedDate = SeedConstants.SeedTimestamp
        });
    }
}

public class AuditModuleConfigConfiguration : IEntityTypeConfiguration<AuditModuleConfig>
{
    public void Configure(EntityTypeBuilder<AuditModuleConfig> builder)
    {
        builder.ToTable("AuditModuleConfigs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.ModuleCode).IsRequired().HasMaxLength(60);
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(c => c.MenuModuleId).IsUnique().HasDatabaseName("UX_AuditModuleConfigs_MenuModuleId");
        builder.HasIndex(c => c.ModuleCode).HasDatabaseName("IX_AuditModuleConfigs_ModuleCode");

        builder.HasOne(c => c.MenuModule)
            .WithMany()
            .HasForeignKey(c => c.MenuModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditUserSessionConfiguration : IEntityTypeConfiguration<AuditUserSession>
{
    public void Configure(EntityTypeBuilder<AuditUserSession> builder)
    {
        builder.ToTable("AuditUserSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UsernameOrEmail).HasMaxLength(256);
        builder.Property(s => s.ExitReason).HasMaxLength(100);
        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(500);
        builder.Property(s => s.Platform).HasMaxLength(50);

        builder.HasIndex(s => s.UserId).HasDatabaseName("IX_AuditUserSessions_UserId");
        builder.HasIndex(s => s.LoginTimeUtc).HasDatabaseName("IX_AuditUserSessions_LoginTimeUtc");
    }
}

public class AuditActivityLogConfiguration : IEntityTypeConfiguration<AuditActivityLog>
{
    public void Configure(EntityTypeBuilder<AuditActivityLog> builder)
    {
        builder.ToTable("AuditActivityLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UsernameOrEmail).HasMaxLength(256);
        builder.Property(a => a.ModuleCode).IsRequired().HasMaxLength(60);
        builder.Property(a => a.FormName).HasMaxLength(150);
        builder.Property(a => a.RoutePath).HasMaxLength(300);
        builder.Property(a => a.Platform).HasMaxLength(50);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => a.UserId).HasDatabaseName("IX_AuditActivityLogs_UserId");
        builder.HasIndex(a => a.ModuleCode).HasDatabaseName("IX_AuditActivityLogs_ModuleCode");
        builder.HasIndex(a => a.EnteredAtUtc).HasDatabaseName("IX_AuditActivityLogs_EnteredAtUtc");
        builder.HasIndex(a => a.SessionId).HasDatabaseName("IX_AuditActivityLogs_SessionId");
    }
}

public class AuditDataLogConfiguration : IEntityTypeConfiguration<AuditDataLog>
{
    public void Configure(EntityTypeBuilder<AuditDataLog> builder)
    {
        builder.ToTable("AuditDataLogs");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.UsernameOrEmail).HasMaxLength(256);
        builder.Property(d => d.Action).IsRequired().HasMaxLength(30);
        builder.Property(d => d.EntityName).IsRequired().HasMaxLength(120);
        builder.Property(d => d.EntityId).HasMaxLength(128);
        builder.Property(d => d.ModuleCode).HasMaxLength(60);
        builder.Property(d => d.ChangedColumns).HasMaxLength(1000);
        builder.Property(d => d.IpAddress).HasMaxLength(64);
        builder.Property(d => d.Endpoint).HasMaxLength(300);

        builder.HasIndex(d => d.TimestampUtc).HasDatabaseName("IX_AuditDataLogs_TimestampUtc");
        builder.HasIndex(d => new { d.EntityName, d.EntityId }).HasDatabaseName("IX_AuditDataLogs_Entity");
        builder.HasIndex(d => d.UserId).HasDatabaseName("IX_AuditDataLogs_UserId");
    }
}

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("ErrorLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(60);
        builder.Property(e => e.Severity).IsRequired().HasMaxLength(30);
        builder.Property(e => e.ExceptionType).HasMaxLength(256);
        builder.Property(e => e.Message).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.RequestPath).HasMaxLength(300);
        builder.Property(e => e.RequestMethod).HasMaxLength(20);
        builder.Property(e => e.UsernameOrEmail).HasMaxLength(256);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.ResolvedBy).HasMaxLength(100);

        builder.HasIndex(e => e.TimestampUtc).HasDatabaseName("IX_ErrorLogs_TimestampUtc");
        builder.HasIndex(e => e.Source).HasDatabaseName("IX_ErrorLogs_Source");
        builder.HasIndex(e => e.IsResolved).HasDatabaseName("IX_ErrorLogs_IsResolved");
    }
}

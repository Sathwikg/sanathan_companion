using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services.BackgroundJobs;
using Sanathana.Companion.Infrastructure.Audit;
using Sanathana.Companion.Infrastructure.BackgroundJobs;

namespace Sanathana.Companion.Infrastructure.BackgroundJobs;

/// <summary>
/// Extension methods for configuring Hangfire background job processing.
/// </summary>
public static class HangfireConfiguration
{
    /// <summary>
    /// Adds Hangfire with PostgreSQL storage to the service collection.
    /// </summary>
    public static IServiceCollection AddHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>();

        // Only configure Hangfire if explicitly enabled in configuration
        if (options == null || !options.Enabled)
        {
            return services;
        }

        // Get connection string for Hangfire storage
        var connectionString = options.StorageConnectionString 
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // No connection string available - skip Hangfire configuration silently
            return services;
        }

        // Configure Hangfire with PostgreSQL storage
        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

#pragma warning disable CS0618 // Obsolete API but still works
            config.UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions
            {
                SchemaName = options.SchemaName,
                QueuePollInterval = TimeSpan.FromSeconds(options.QueuePollIntervalSeconds),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                PrepareSchemaIfNecessary = true,
                UseNativeDatabaseTransactions = true
            });
#pragma warning restore CS0618

            // Configure job filters
            config.UseFilter(new AutomaticRetryAttribute { Attempts = options.AutomaticRetryAttempts });
            // JobExecutionTimeoutAttribute doesn't exist in Hangfire 1.8 - timeout handled by job cancellation
        });

        // Add Hangfire server with configured worker count
        services.AddHangfireServer(serverOptions =>
        {
            serverOptions.WorkerCount = options.WorkerCount ?? Environment.ProcessorCount;
            serverOptions.Queues = new[] { "default", "notifications", "reminders", "cleanup", "sync" };
            serverOptions.ServerName = $"sanathana-api-{Environment.MachineName}";
        });

        // Register background job services
        services.AddScoped<NotificationDeliveryJob>();
        services.AddScoped<ReminderSyncJob>();
        services.AddScoped<CleanupJob>();
        services.AddScoped<DataSyncJob>();

        return services;
    }

    /// <summary>
    /// Configures recurring jobs for Hangfire.
    /// </summary>
    public static void ConfigureRecurringJobs(IServiceProvider serviceProvider, HangfireOptions options)
    {
        if (!options.Enabled) return;

        var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();

        // Cleanup job - runs daily at 2:00 AM IST
        recurringJobManager.AddOrUpdate<CleanupJob>(
            "cleanup-daily",
            job => job.RunAllCleanupAsync(default),
            "0 2 * * *", // Daily at 2:00 AM
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time") });

        // Reminder sync - runs every 30 minutes
        recurringJobManager.AddOrUpdate<ReminderSyncJob>(
            "reminder-sync",
            job => job.SyncAllUserRemindersAsync(default),
            "*/30 * * * *", // Every 30 minutes
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time") });

        // Data sync - runs every 6 hours
        recurringJobManager.AddOrUpdate<DataSyncJob>(
            "data-sync",
            job => job.RunAllSyncAsync(default),
            "0 */6 * * *", // Every 6 hours
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time") });

        // Notification delivery - runs every 5 minutes (when FCM/APNs is implemented)
        recurringJobManager.AddOrUpdate<NotificationDeliveryJob>(
            "notification-delivery",
            job => job.ProcessPendingNotificationsAsync(default),
            "*/5 * * * *", // Every 5 minutes
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time") });

        // Audit config cache reload - runs every minute (already handled by AuditMaintenanceService)
        // But we can also trigger it via Hangfire for multi-instance deployments
        recurringJobManager.AddOrUpdate<IAuditConfigCache>(
            "audit-config-reload",
            cache => cache.ReloadAsync(default),
            "* * * * *", // Every minute
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time") });
    }

    /// <summary>
    /// Enqueues a job to sync reminders for a specific user (triggered on preference change).
    /// </summary>
    public static void EnqueueUserReminderSync(this IBackgroundJobClient client, Guid userId)
    {
        client.Enqueue<ReminderSyncJob>(job => job.OnUserPreferencesChangedAsync(userId, default));
    }

    /// <summary>
    /// Enqueues a test notification job.
    /// </summary>
    public static void EnqueueTestNotification(this IBackgroundJobClient client, Guid userId, string title, string body)
    {
        client.Enqueue<NotificationDeliveryJob>(job => job.SendTestNotificationAsync(userId, title, body, default));
    }

    /// <summary>
    /// Schedules a cleanup job to run immediately.
    /// </summary>
    public static void TriggerCleanup(this IBackgroundJobClient client)
    {
        client.Enqueue<CleanupJob>(job => job.RunAllCleanupAsync(default));
    }

    /// <summary>
    /// Schedules a data sync job to run immediately.
    /// </summary>
    public static void TriggerDataSync(this IBackgroundJobClient client)
    {
        client.Enqueue<DataSyncJob>(job => job.RunAllSyncAsync(default));
    }
}
using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services.BackgroundJobs;

/// <summary>
/// Background job for syncing user notification preferences and reminders.
/// </summary>
/// <remarks>
/// This job ensures that user reminder schedules are kept in sync with their
/// notification preferences. It can be triggered on preference changes or run periodically.
/// </remarks>
public sealed class ReminderSyncJob
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ReminderSyncJob> _logger;

    public ReminderSyncJob(
        IUnitOfWork uow,
        INotificationService notificationService,
        ILogger<ReminderSyncJob> logger)
    {
        _uow = uow;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Syncs reminders for all users with active notification preferences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of users synced.</returns>
    public async Task<int> SyncAllUserRemindersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting reminder sync job for all users");

        try
        {
            var users = await _uow.Users.GetUsersWithNotificationPrefsAsync(cancellationToken);
            var syncedCount = 0;

            foreach (var user in users)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    await SyncUserRemindersAsync(user.UserId, cancellationToken);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync reminders for user {UserId}", user.UserId);
                }
            }

            _logger.LogInformation("Reminder sync job completed. Synced {Count} users", syncedCount);
            return syncedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reminder sync job failed");
            throw;
        }
    }

    /// <summary>
    /// Syncs reminders for a specific user.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SyncUserRemindersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await _notificationService.GetMySettingsAsync(userId, cancellationToken);
        
        // The actual reminder scheduling happens on the mobile device via the
        // MauiReminderScheduler. This job would typically:
        // 1. Notify the mobile app to re-sync (via push notification or SignalR)
        // 2. Or store the computed reminder schedule for the app to fetch
        
        // For now, we log the computed reminders for visibility
        var activeReminders = settings.Items.Where(i => i.IsActiveNow).ToList();
        _logger.LogDebug("User {UserId} has {Count} active reminders", userId, activeReminders.Count);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Triggered when a user updates their notification preferences.
    /// </summary>
    public async Task OnUserPreferencesChangedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {UserId} notification preferences changed, syncing reminders", userId);
        await SyncUserRemindersAsync(userId, cancellationToken);
    }
}
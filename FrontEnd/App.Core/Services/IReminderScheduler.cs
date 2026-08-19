namespace App.Core.Services;

using App.Core.Models;

/// <summary>Schedules the seeker's daily reminders with the operating system. One per host.</summary>
/// <remarks>
/// The API models what a seeker wants to be reminded about but has no delivery mechanism at all —
/// no push registration, no scheduler, no background job. Locally scheduled notifications are the
/// only thing that can close that gap from the client, and they are also the cheapest: the OS holds
/// the schedule and wakes the app, so nothing polls and nothing runs while the phone sleeps.
/// </remarks>
public interface IReminderScheduler
{
    /// <summary>
    /// True when the OS will actually show a notification. Asks the seeker the first time; on
    /// Android 13+ and every iOS version a refusal is final until they change it in system settings.
    /// </summary>
    Task<bool> IsAllowedAsync();

    /// <summary>Replaces every scheduled reminder with the ones these preferences imply.</summary>
    Task SyncAsync(MyNotificationSettings? settings);

    /// <summary>Cancels everything. Used on sign-out, so one seeker's reminders never reach the next.</summary>
    Task ClearAsync();
}

/// <summary>
/// What the web host gets. The browser has no equivalent of a scheduled local notification that
/// survives the tab being closed, so the honest implementation is to do nothing.
/// </summary>
public sealed class NoOpReminderScheduler : IReminderScheduler
{
    public Task<bool> IsAllowedAsync() => Task.FromResult(false);
    public Task SyncAsync(MyNotificationSettings? settings) => Task.CompletedTask;
    public Task ClearAsync() => Task.CompletedTask;
}

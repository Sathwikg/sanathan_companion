namespace App.Core.Services;

using App.Core.Models;

/// <summary>Schedules the seeker's daily reminders with the operating system. One per host.</summary>
/// <remarks>
/// The server now pushes most notifications itself. What is left for the phone is the plain daily
/// reminder, and the sadhana reminder on a phone push cannot reach (see <see cref="ReminderPlanner"/>).
/// Locally scheduled notifications are the cheapest way to deliver those: the OS holds the schedule
/// and wakes the app, so nothing polls and nothing runs while the phone sleeps.
/// </remarks>
public interface IReminderScheduler
{
    /// <summary>
    /// True when the OS will actually show a notification. Asks the seeker the first time; on
    /// Android 13+ and every iOS version a refusal is final until they change it in system settings.
    /// </summary>
    Task<bool> IsAllowedAsync();

    /// <summary>Replaces every scheduled reminder with the ones these preferences imply.</summary>
    /// <param name="pushDelivers">The server reaches this phone by push, so it sends the timed types itself.</param>
    /// <param name="label">A label lookup in the seeker's language; null when there is none to offer.</param>
    Task SyncAsync(MyNotificationSettings? settings, bool pushDelivers = false, Func<string, string?>? label = null);

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
    public Task SyncAsync(MyNotificationSettings? settings, bool pushDelivers = false, Func<string, string?>? label = null) => Task.CompletedTask;
    public Task ClearAsync() => Task.CompletedTask;
}

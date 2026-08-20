using App.Core.Models;
using App.Core.Services;

#if ANDROID
using App.Mobile.Platforms.Android.Notifications;
#elif IOS || MACCATALYST
using Foundation;
using UserNotifications;
#endif

namespace App.Mobile.Services;

/// <summary>
/// Schedules the seeker's reminders with the operating system.
/// </summary>
/// <remarks>
/// Both platforms hand the schedule to the OS and then do nothing: iOS through a repeating
/// calendar trigger, Android through an inexact repeating alarm. Nothing polls, no background
/// service runs, and the app is not woken between reminders — which is both the cheapest option
/// for the battery and the only one that survives Doze and App Standby.
/// <para>
/// Times are treated as device-local wall clock. See <see cref="ReminderPlanner"/> for why.
/// </para>
/// </remarks>
public class MauiReminderScheduler : IReminderScheduler
{
#if ANDROID
    /// <summary>
    /// The application context, which the Android bindings declare as nullable.
    /// </summary>
    /// <remarks>
    /// It is only null before the Application object exists, which should never be true while a
    /// seeker is looking at a settings screen. Treated as "cannot schedule" rather than dereferenced
    /// anyway, because an unhandled exception out of a permission check does not surface as an error
    /// in a Blazor Hybrid host — it tears down the WebView.
    /// </remarks>
    private static global::Android.Content.Context? AndroidContext => global::Android.App.Application.Context;
#endif

    public async Task<bool> IsAllowedAsync()
    {
#if ANDROID
        // Android 13+ will silently drop every notification without this. Below 33 it is granted
        // at install time and the request returns immediately.
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();

        if (status != PermissionStatus.Granted) return false;

        var context = AndroidContext;
        if (context is null) return false;

        // The runtime permission is only half the answer. A seeker can switch the app's
        // notifications off in system Settings on ANY Android version — on 12 and below that is
        // the only control there is, and the permission check would still report Granted. Without
        // this the app would insist a reminder "Would notify now" while the OS dropped every one.
        //
        // From() is declared nullable by the bindings, so the result is checked rather than
        // dereferenced: no manager means nothing can be promised about notifications, which is
        // "not allowed", not a crash out of a settings screen.
        var manager = AndroidX.Core.App.NotificationManagerCompat.From(context);
        return manager is not null && manager.AreNotificationsEnabled();
#elif IOS || MACCATALYST
        var (granted, _) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);

        return granted;
#else
        return await Task.FromResult(false);
#endif
    }

    public async Task SyncAsync(MyNotificationSettings? settings)
    {
        var reminders = ReminderPlanner.Plan(settings);

        // Asking only when there is something to schedule keeps the permission prompt off the
        // first launch, where it would arrive with no context and be refused.
        if (reminders.Count == 0)
        {
            await ClearAsync();
            return;
        }

        if (!await IsAllowedAsync()) return;

#if ANDROID
        var context = AndroidContext;
        if (context is null) return;

        ReminderNotifier.EnsureChannel(context);
        AndroidReminderAlarms.Sync(
            context,
            reminders.Select(r => new AndroidReminderAlarms.CachedReminder(r.Id, r.Title, r.Body, r.Hour, r.Minute)).ToList());
#elif IOS || MACCATALYST
        var center = UNUserNotificationCenter.Current;
        center.RemoveAllPendingNotificationRequests();

        foreach (var reminder in reminders)
        {
            var content = new UNMutableNotificationContent
            {
                Title = reminder.Title,
                Sound = UNNotificationSound.Default
            };
            if (reminder.Body is not null) content.Body = reminder.Body;

            // DateComponents with only hour and minute repeats daily at that wall-clock time, so
            // iOS re-anchors it across timezone changes and daylight saving on its own.
            var components = new NSDateComponents { Hour = reminder.Hour, Minute = reminder.Minute };
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, repeats: true);

            center.AddNotificationRequest(
                UNNotificationRequest.FromIdentifier(reminder.Id, content, trigger),
                error => { /* one rejected reminder must not stop the rest */ });
        }
#endif
        await Task.CompletedTask;
    }

    public Task ClearAsync()
    {
#if ANDROID
        if (AndroidContext is { } context) AndroidReminderAlarms.CancelAll(context);
#elif IOS || MACCATALYST
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
#endif
        return Task.CompletedTask;
    }
}

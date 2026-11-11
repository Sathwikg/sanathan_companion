using App.Core.Models;

namespace App.Core.Services;

/// <summary>How a notification type names itself on the settings screens and in the bell's panel.</summary>
public static class NotificationText
{
    /// <summary>
    /// The type's name: what an administrator typed if they typed anything, otherwise the built-in
    /// name in the seeker's language. A plain reminder has no built-in name; it is called after its form.
    /// </summary>
    public static string Title(LocalizationState loc, MyNotificationItem item)
        => item.HasCustomText || !IsBuiltIn(item) ? item.Title : loc.T($"push.{item.Kind}.name", item.Title);

    public static string? Description(LocalizationState loc, MyNotificationItem item)
        => item.HasCustomText || !IsBuiltIn(item) ? item.Description : loc.T($"push.{item.Kind}.desc", item.Description ?? string.Empty);

    private static bool IsBuiltIn(MyNotificationItem item)
        => !string.IsNullOrEmpty(item.Kind) && item.Kind != "reminder";

    /// <summary>How long ago a notification arrived, in the fewest words that are still true.</summary>
    public static string Ago(LocalizationState loc, DateTime sentAtUtc, DateTime nowUtc)
    {
        var age = nowUtc - DateTime.SpecifyKind(sentAtUtc, DateTimeKind.Utc);
        if (age < TimeSpan.FromMinutes(1)) return loc.T("nav.notifJustNow", "Just now");
        if (age < TimeSpan.FromHours(1)) return loc.TF("nav.notifMinutesAgo", "{0} min ago", (int)age.TotalMinutes);
        if (age < TimeSpan.FromHours(24)) return loc.TF("nav.notifHoursAgo", "{0} h ago", (int)age.TotalHours);
        if (age < TimeSpan.FromHours(48)) return loc.T("nav.notifYesterday", "Yesterday");

        // Dates stay in English digits and month names by design, like every other date in the app.
        return sentAtUtc.ToLocalTime().ToString("d MMM");
    }
}

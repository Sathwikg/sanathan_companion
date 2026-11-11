using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Domain.Entities;
using PanchangamDay = Sanathana.Companion.Domain.Entities.Panchangam;

namespace Sanathana.Companion.Application.Notifications;

/// <summary>How a notification type decides its moment.</summary>
public enum NotificationSchedule
{
    /// <summary>A plain daily reminder the phone schedules with its own alarm. The server never sends it.</summary>
    Device,

    /// <summary>Once a day at a time the seeker picks (falling back to the configured time).</summary>
    UserTime,

    /// <summary>Once a day at the configured time, when the type's own condition holds.</summary>
    DailyAt,

    /// <summary>A configured number of minutes before a Panchangam window opens.</summary>
    BeforeWindow,

    /// <summary>Only when an administrator sends it.</summary>
    Manual
}

/// <summary>The Android notification channel a type is shown under: the seeker's own switches in system settings.</summary>
public static class NotificationChannels
{
    public const string Sadhana = "sadhana";
    public const string Panchangam = "panchangam";
    public const string Festivals = "festivals";
    public const string General = "general";
}

/// <summary>One built-in notification type: what it is, where it belongs and how it behaves until an administrator says otherwise.</summary>
/// <param name="Code">Stored in NotificationConfig.Kind and in every log row. Never rename one.</param>
/// <param name="ModuleCode">The form it belongs to; its config row hangs off that form's menu row.</param>
/// <param name="Name">English name on the settings screens. The translated one is the "push.{Code}.name" label.</param>
/// <param name="Description">English one-liner for the settings screens ("push.{Code}.desc").</param>
/// <param name="Route">The page a tap opens.</param>
/// <param name="DefaultOn">Whether seekers get it until they say otherwise.</param>
public sealed record NotificationKind(
    string Code,
    string ModuleCode,
    NotificationSchedule Schedule,
    string Name,
    string Description,
    string Route,
    string Channel,
    bool DefaultOn,
    TimeOnly? SendTime = null,
    int? LeadMinutes = null,
    int? Threshold = null)
{
    /// <summary>The Panchangam column a <see cref="NotificationSchedule.BeforeWindow"/> type watches.</summary>
    public Func<PanchangamDay, string?>? Window { get; init; }

    /// <summary>Whether the window is one to seek out rather than one to avoid; picks the wording and nothing else.</summary>
    public bool Auspicious { get; init; }

    public string TitleKey => $"push.{Code}.title";
    public string BodyKey => $"push.{Code}.body";
}

/// <summary>
/// Every notification type the server knows how to raise. Adding one here, with its wording in the
/// "push" language files and its rule in the engine, is the whole of adding a notification.
/// </summary>
public static class NotificationKinds
{
    public const string Reminder = NotificationConfig.ReminderKind;

    public const string SadhanaReminder = "sadhanaReminder";
    public const string StreakAtRisk = "streakAtRisk";
    public const string StreakMissed = "streakMissed";

    public const string PanchangamDaily = "panchangamDaily";
    public const string RahuKalam = "rahuKalam";
    public const string Yamagandam = "yamagandam";
    public const string Gulika = "gulika";
    public const string Durmuhurtham = "durmuhurtham";
    public const string Varjyam = "varjyam";
    public const string AbhijitMuhurtham = "abhijitMuhurtham";
    public const string AmruthaKalam = "amruthaKalam";

    public const string FestivalToday = "festivalToday";
    public const string FestivalTomorrow = "festivalTomorrow";
    public const string CalendarEvent = "calendarEvent";

    public const string Announcement = "announcement";

    /// <summary>The built-in types, in the order the settings screens list them.</summary>
    public static readonly IReadOnlyList<NotificationKind> All = new NotificationKind[]
    {
        new(SadhanaReminder, ModuleCodes.Sadhana, NotificationSchedule.UserTime,
            "Daily Sadhana Reminder",
            "A gentle call to your japa at the time you choose. Skipped on days you have already practised.",
            "/sadhana", NotificationChannels.Sadhana, DefaultOn: true, SendTime: new TimeOnly(6, 0)),

        new(StreakAtRisk, ModuleCodes.Sadhana, NotificationSchedule.DailyAt,
            "Streak About to Break",
            "An evening nudge when you have a streak going and have not practised yet today.",
            "/sadhana", NotificationChannels.Sadhana, DefaultOn: true, SendTime: new TimeOnly(20, 0), Threshold: 2),

        new(StreakMissed, ModuleCodes.Sadhana, NotificationSchedule.DailyAt,
            "Missed Streak",
            "The morning after a streak breaks, an invitation to begin again.",
            "/sadhana", NotificationChannels.Sadhana, DefaultOn: true, SendTime: new TimeOnly(8, 0), Threshold: 3),

        new(PanchangamDaily, ModuleCodes.Panchangam, NotificationSchedule.DailyAt,
            "Today's Panchangam",
            "Each morning: the tithi, the nakshatram and the day's Rahu Kalam for your region.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: true, SendTime: new TimeOnly(6, 30)),

        new(RahuKalam, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Rahu Kalam Warning",
            "A warning shortly before Rahu Kalam begins, so nothing new is started in it.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: true, LeadMinutes: 10) { Window = p => p.RahuKalam },

        new(Yamagandam, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Yamagandam Warning",
            "A warning shortly before Yamagandam begins.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: false, LeadMinutes: 10) { Window = p => p.Yamagandam },

        new(Gulika, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Gulika Kalam Warning",
            "A warning shortly before Gulika Kalam begins.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: false, LeadMinutes: 10) { Window = p => p.Gulika },

        new(Durmuhurtham, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Durmuhurtham Warning",
            "A warning shortly before each Durmuhurtham of the day.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: false, LeadMinutes: 10) { Window = p => p.Durmuhurtham },

        new(Varjyam, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Varjyam Warning",
            "A warning shortly before Varjyam begins.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: false, LeadMinutes: 10) { Window = p => p.Varjyam },

        new(AbhijitMuhurtham, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Abhijit Muhurtham",
            "A note shortly before the day's most auspicious muhurtham opens.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: false, LeadMinutes: 5) { Window = p => p.AbhijitMuhurtham, Auspicious = true },

        new(AmruthaKalam, ModuleCodes.Panchangam, NotificationSchedule.BeforeWindow,
            "Amrutha Kalam",
            "A note shortly before Amrutha Kalam, a good time to begin something important.",
            "/panchangam", NotificationChannels.Panchangam, DefaultOn: false, LeadMinutes: 5) { Window = p => p.AmruthaKalam, Auspicious = true },

        new(FestivalToday, ModuleCodes.Calendar, NotificationSchedule.DailyAt,
            "Festival Today",
            "On the morning of a festival observed in your region.",
            "/calendar", NotificationChannels.Festivals, DefaultOn: true, SendTime: new TimeOnly(7, 0)),

        new(FestivalTomorrow, ModuleCodes.Calendar, NotificationSchedule.DailyAt,
            "Festival Tomorrow",
            "The evening before a festival, in time to prepare for the puja.",
            "/calendar", NotificationChannels.Festivals, DefaultOn: true, SendTime: new TimeOnly(18, 0)),

        new(CalendarEvent, ModuleCodes.Calendar, NotificationSchedule.DailyAt,
            "My Calendar Marks",
            "On the morning of a vratam, puja or anniversary you marked on your own calendar.",
            "/calendar", NotificationChannels.Festivals, DefaultOn: true, SendTime: new TimeOnly(7, 0)),

        new(Announcement, ModuleCodes.MyNotifications, NotificationSchedule.Manual,
            "Announcements",
            "Occasional news about the app from the team.",
            "/", NotificationChannels.General, DefaultOn: true),
    };

    private static readonly Dictionary<string, NotificationKind> ByCode =
        All.ToDictionary(k => k.Code, StringComparer.Ordinal);

    /// <summary>The built-in type with this code, or null for the plain reminder and for unknown codes.</summary>
    public static NotificationKind? Find(string? code)
        => code is not null && ByCode.TryGetValue(code, out var kind) ? kind : null;

    /// <summary>How a stored config row is scheduled; anything the catalogue does not know is a device reminder.</summary>
    public static NotificationSchedule ScheduleOf(string? code) => Find(code)?.Schedule ?? NotificationSchedule.Device;
}

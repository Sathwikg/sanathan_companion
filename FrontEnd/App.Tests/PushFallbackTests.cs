using App.Core.Models;
using App.Core.Services;

namespace App.Tests;

/// <summary>
/// What the phone still schedules for itself now that the server pushes: the plain reminders
/// always, the sadhana reminder only while push cannot reach this phone, and nothing else ever.
/// A mistake here is a reminder that arrives twice, or not at all.
/// </summary>
public class PushFallbackTests
{
    private static MyNotificationItem Item(string kind, string schedule, string? from = null, string? to = null, string? defaultTime = null) => new()
    {
        ConfigId = Guid.NewGuid(),
        Title = "Daily Sadhana Reminder",
        Description = "A gentle call to your japa at the time you choose.",
        ModuleName = "Today's Sadhana",
        Kind = kind,
        Schedule = schedule,
        IsEnabled = true,
        FromTime = from is null ? null : TimeOnly.Parse(from),
        ToTime = to is null ? null : TimeOnly.Parse(to),
        DefaultTime = defaultTime is null ? null : TimeOnly.Parse(defaultTime),
    };

    private static MyNotificationSettings Settings(params MyNotificationItem[] items)
        => new() { MasterEnabled = true, Items = items.ToList() };

    [Fact]
    public void While_push_cannot_reach_the_phone_the_sadhana_reminder_is_scheduled_here()
    {
        var plan = ReminderPlanner.Plan(Settings(Item("sadhanaReminder", "UserTime", from: "05:30", defaultTime: "06:00")), pushDelivers: false);

        var reminder = Assert.Single(plan);
        Assert.Equal((5, 30), (reminder.Hour, reminder.Minute));   // the seeker's own time wins
    }

    [Fact]
    public void A_seeker_who_never_chose_a_time_gets_the_administrators_default()
    {
        var plan = ReminderPlanner.Plan(Settings(Item("sadhanaReminder", "UserTime", defaultTime: "06:00")));

        Assert.Equal((6, 0), (Assert.Single(plan).Hour, Assert.Single(plan).Minute));
    }

    [Fact]
    public void Once_push_reaches_the_phone_the_server_sends_it_and_the_phone_does_not()
    {
        // Both at once would be the same reminder twice, a minute apart.
        Assert.Empty(ReminderPlanner.Plan(Settings(Item("sadhanaReminder", "UserTime", from: "05:30", defaultTime: "06:00")), pushDelivers: true));
    }

    [Theory]
    [InlineData("rahuKalam", "BeforeWindow")]
    [InlineData("streakAtRisk", "DailyAt")]
    [InlineData("panchangamDaily", "DailyAt")]
    [InlineData("announcement", "Manual")]
    public void A_type_only_the_server_can_judge_is_never_scheduled_on_the_phone(string kind, string schedule)
    {
        // Even with a window set: for these it is "only between these hours", not "at this hour",
        // and whether it is due depends on a streak or a Panchangam the phone does not hold.
        var item = Item(kind, schedule, from: "08:00", to: "20:00");

        Assert.Empty(ReminderPlanner.Plan(Settings(item), pushDelivers: false));
        Assert.Empty(ReminderPlanner.Plan(Settings(item), pushDelivers: true));
    }

    [Fact]
    public void A_plain_reminder_is_the_phones_own_whether_or_not_push_works()
    {
        var item = Item("reminder", "Device", from: "07:00", to: "08:00");

        Assert.Single(ReminderPlanner.Plan(Settings(item), pushDelivers: false));
        Assert.Single(ReminderPlanner.Plan(Settings(item), pushDelivers: true));
    }

    [Fact]
    public void An_older_server_that_names_no_schedule_is_read_as_plain_reminders()
    {
        var item = Item("reminder", schedule: "", from: "07:00", to: "08:00");

        Assert.Single(ReminderPlanner.Plan(Settings(item)));
    }

    [Fact]
    public void The_fallback_speaks_the_pushs_own_words_when_they_can_be_found()
    {
        var labels = new Dictionary<string, string>
        {
            ["push.sadhanaReminder.title"] = "🪔 మీ సాధనకు సమయమైంది",
            ["push.sadhanaReminder.body"] = "నేటి సాధన ప్రారంభించండి."
        };

        var plan = ReminderPlanner.Plan(Settings(Item("sadhanaReminder", "UserTime", defaultTime: "06:00")),
            pushDelivers: false, label: key => labels.GetValueOrDefault(key));

        var reminder = Assert.Single(plan);
        Assert.Equal("🪔 మీ సాధనకు సమయమైంది", reminder.Title);
        Assert.Equal("నేటి సాధన ప్రారంభించండి.", reminder.Body);
    }

    [Fact]
    public void Without_those_words_the_fallback_uses_the_types_own_name()
    {
        var plan = ReminderPlanner.Plan(Settings(Item("sadhanaReminder", "UserTime", defaultTime: "06:00")), label: _ => null);

        Assert.Equal("Daily Sadhana Reminder", Assert.Single(plan).Title);
    }
}

/// <summary>
/// The page a tapped notification opens travels through a push payload, so it is untrusted by the
/// time it reaches the app: only a path inside the app may be followed.
/// </summary>
public class DeepLinksTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/sadhana", true)]
    [InlineData("/sadhana/chant/3f2a", true)]
    [InlineData("/calendar?date=2026-11-12", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("sadhana", false)]                        // not rooted
    [InlineData("//evil.example/steal", false)]           // protocol-relative: another site
    [InlineData("/\\evil.example", false)]                // the backslash spelling of the same
    [InlineData("https://evil.example", false)]
    [InlineData("/go?to=https://evil.example", false)]    // refused outright rather than parsed
    [InlineData("javascript:alert(1)", false)]
    public void Only_a_path_inside_the_app_is_followed(string? route, bool allowed)
        => Assert.Equal(allowed, DeepLinks.IsInAppPath(route));

    [Fact]
    public void A_route_that_arrives_before_anything_is_listening_waits_and_is_taken_once()
    {
        var links = new DeepLinks();

        links.Open("/panchangam");      // the app was closed; no layout exists yet

        Assert.Equal("/panchangam", links.Take());
        Assert.Null(links.Take());      // a re-render must not navigate again
    }

    [Fact]
    public void A_route_that_is_not_in_the_app_is_dropped_without_a_signal()
    {
        var links = new DeepLinks();
        var raised = 0;
        links.Arrived += () => raised++;

        links.Open("https://evil.example");

        Assert.Equal(0, raised);
        Assert.Null(links.Take());
    }
}

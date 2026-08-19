using App.Core.Models;
using App.Core.Services;

namespace App.Tests;

/// <summary>
/// The planner decides what a phone will actually wake a seeker up for, so its rules are pinned
/// here rather than discovered on a device.
/// </summary>
public class ReminderPlannerTests
{
    private static MyNotificationItem Item(
        string title = "Daily Sadhana",
        bool enabled = true,
        bool mandatory = false,
        string? from = "06:00",
        string? to = "07:00",
        string? description = "Time for your practice") => new()
    {
        ConfigId = Guid.NewGuid(),
        Title = title,
        Description = description,
        ModuleName = "Sadhana",
        IsEnabled = enabled,
        IsMandatory = mandatory,
        FromTime = from is null ? null : TimeOnly.Parse(from),
        ToTime = to is null ? null : TimeOnly.Parse(to),
    };

    private static MyNotificationSettings Settings(
        bool master = true,
        bool quiet = false,
        string? quietFrom = null,
        string? quietTo = null,
        params MyNotificationItem[] items) => new()
    {
        MasterEnabled = master,
        QuietHoursEnabled = quiet,
        QuietFrom = quietFrom is null ? null : TimeOnly.Parse(quietFrom),
        QuietTo = quietTo is null ? null : TimeOnly.Parse(quietTo),
        Items = items.ToList(),
    };

    [Fact]
    public void The_window_start_is_the_reminder_time()
    {
        var plan = ReminderPlanner.Plan(Settings(items: Item(from: "06:30", to: "08:00")));

        var reminder = Assert.Single(plan);
        Assert.Equal(6, reminder.Hour);
        Assert.Equal(30, reminder.Minute);
    }

    [Fact]
    public void An_item_with_no_window_is_not_scheduled()
    {
        // "Any time of day" is a permission, not a schedule — inventing an hour would be worse
        // than staying silent.
        Assert.Empty(ReminderPlanner.Plan(Settings(items: Item(from: null, to: null))));
    }

    [Fact]
    public void A_switched_off_item_is_not_scheduled()
        => Assert.Empty(ReminderPlanner.Plan(Settings(items: Item(enabled: false))));

    [Fact]
    public void The_master_switch_silences_everything_optional()
        => Assert.Empty(ReminderPlanner.Plan(Settings(master: false, items: Item())));

    [Fact]
    public void A_mandatory_item_survives_the_master_switch_and_an_opt_out()
    {
        var plan = ReminderPlanner.Plan(Settings(master: false, items: Item(enabled: false, mandatory: true)));

        Assert.Single(plan);
    }

    [Fact]
    public void Quiet_hours_outrank_even_a_mandatory_item()
    {
        // Matches the server's Resolve(): quiet hours are checked first, so the app stays silent
        // while the seeker rests. If the two disagreed, the panel would claim a reminder is
        // active while the phone stayed quiet.
        var plan = ReminderPlanner.Plan(Settings(
            quiet: true, quietFrom: "22:00", quietTo: "07:00",
            items: Item(mandatory: true, from: "06:00", to: "08:00")));

        Assert.Empty(plan);
    }

    [Fact]
    public void Quiet_hours_wrap_past_midnight()
    {
        var settings = Settings(true, true, "22:00", "06:00",
            Item(title: "Night", from: "23:30", to: "23:59"),
            Item(title: "Morning", from: "06:30", to: "07:00"));

        var plan = ReminderPlanner.Plan(settings);

        Assert.Equal("Morning", Assert.Single(plan).Title);
    }

    [Fact]
    public void An_incomplete_quiet_window_silences_nothing()
    {
        // Half a window contains no time at all, the same rule the server's TimeWindow applies.
        var plan = ReminderPlanner.Plan(Settings(quiet: true, quietFrom: "22:00", quietTo: null, items: Item()));

        Assert.Single(plan);
    }

    [Fact]
    public void A_titleless_item_falls_back_to_its_module_name()
    {
        var plan = ReminderPlanner.Plan(Settings(items: Item(title: "  ")));

        Assert.Equal("Sadhana", Assert.Single(plan).Title);
    }

    [Fact]
    public void A_blank_description_becomes_no_body_rather_than_an_empty_line()
    {
        var plan = ReminderPlanner.Plan(Settings(items: Item(description: "   ")));

        Assert.Null(Assert.Single(plan).Body);
    }

    [Fact]
    public void The_id_is_stable_for_a_type_so_a_resync_replaces_rather_than_duplicates()
    {
        var item = Item();
        var first = Assert.Single(ReminderPlanner.Plan(Settings(items: item)));
        var second = Assert.Single(ReminderPlanner.Plan(Settings(items: item)));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(item.ConfigId.ToString("N"), first.Id);
    }

    [Theory]
    [InlineData("3f2504e04f8911d39a0c0305e82c3301", 1600433800)]
    [InlineData("00000000000000000000000000000000", 1892181061)]
    [InlineData("", 2166136261 & 0x7FFFFFFF)]
    public void StableCode_is_the_same_value_in_every_process(string id, int expected)
    {
        // Pinned to literals on purpose. string.GetHashCode() would satisfy any self-consistent
        // assertion while still being randomised per process — which is exactly the bug this
        // guards: an alarm scheduled in one run could not be cancelled in the next, so every
        // launch would leave the old one running and add a duplicate.
        Assert.Equal(expected, ReminderPlanner.StableCode(id));
    }

    [Fact]
    public void StableCode_is_never_negative_because_Android_ids_cannot_be()
    {
        foreach (var id in Enumerable.Range(0, 200).Select(_ => Guid.NewGuid().ToString("N")))
            Assert.True(ReminderPlanner.StableCode(id) >= 0);
    }

    [Fact]
    public void StableCode_separates_different_reminders()
    {
        var codes = Enumerable.Range(0, 500)
            .Select(_ => ReminderPlanner.StableCode(Guid.NewGuid().ToString("N")))
            .ToHashSet();

        // Collisions would make two reminders overwrite each other's alarm and notification.
        Assert.Equal(500, codes.Count);
    }

    [Fact]
    public void No_settings_means_no_reminders_rather_than_a_crash()
        => Assert.Empty(ReminderPlanner.Plan(null));

    [Fact]
    public void Several_enabled_items_all_schedule()
    {
        var plan = ReminderPlanner.Plan(Settings(true, false, null, null,
            Item(title: "A", from: "06:00"),
            Item(title: "B", from: "12:30"),
            Item(title: "C", from: "19:15")));

        Assert.Equal(3, plan.Count);
        Assert.Equal([(6, 0), (12, 30), (19, 15)], plan.Select(r => (r.Hour, r.Minute)));
    }
}

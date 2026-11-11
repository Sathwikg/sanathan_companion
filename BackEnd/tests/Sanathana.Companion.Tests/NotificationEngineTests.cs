using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Notifications;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Localization;
using Sanathana.Companion.Infrastructure.Notifications;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The notification engine against a real (in-memory) database and a recording stand-in for
/// Firebase: who is due what at a pinned instant, and that each notification is raised once.
/// </summary>
public class NotificationEngineTests
{
    // 2026-11-11 is a Wednesday. IST is UTC+5:30, so 00:30 UTC is 06:00 in India.
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static DateTime Ist(int hour, int minute, DateOnly? date = null)
        => DateTime.SpecifyKind((date ?? Today).ToDateTime(new TimeOnly(hour, minute)) - TimeSpan.FromHours(5.5), DateTimeKind.Utc);

    /// <summary>Everything a test needs, wired the way the API wires it, minus Firebase.</summary>
    private sealed class Rig : IDisposable
    {
        public TestHarness Harness { get; } = new();
        public FakeTransport Transport { get; } = new();
        public FakeAccess Access { get; } = new();
        public NotificationStore Store { get; }
        public NotificationService Service { get; }
        public NotificationEngine Engine { get; }
        public Guid RegionId { get; } = Guid.NewGuid();

        public Rig()
        {
            Store = new NotificationStore(Harness.Context);
            Service = new NotificationService(Harness.UnitOfWork, Transport);
            var texts = new PushTextProvider(Harness.UnitOfWork, new EmbeddedLocalizationSeedSource(), new EnglishOnlyCatalog(), new PushTextCache());
            Engine = new NotificationEngine(Store, Transport, texts, Access, Service,
                Options.Create(new PushNotificationOptions { Enabled = true, GraceMinutes = 30 }),
                NullLogger<NotificationEngine>.Instance);

            Harness.Context.Regions.Add(new Region { Id = RegionId, Name = "Hyderabad", Latitude = 17.385, Longitude = 78.4867, IsActive = true });
            Harness.Context.SaveChanges();
        }

        /// <summary>A seeker with one Android phone, in India, in the test region.</summary>
        public async Task<Guid> AddSeekerAsync(string name = "Lakshmi Devi", string? timeZone = "Asia/Kolkata", string? language = "en", int roleId = SeedConstants.SanathanRoleId)
        {
            var userId = Guid.NewGuid();
            Harness.Context.Users.Add(new User
            {
                UserId = userId,
                FullName = name,
                Email = $"{userId:N}@example.org",
                MobileNumber = userId.ToString("N")[..10],
                PasswordHash = "x",
                RoleId = roleId,
                DefaultRegionId = RegionId
            });
            await Harness.Context.SaveChangesAsync();
            await Store.UpsertDeviceAsync(userId, 1, "token-" + userId.ToString("N"), "Pixel 8", "1.3.0", timeZone, language);
            return userId;
        }

        public async Task SetStreakAsync(Guid userId, int current, DateOnly lastPractice)
        {
            Harness.Context.SadhanaStreaks.Add(new SadhanaStreak { UserId = userId, CurrentStreak = current, LongestStreak = current, LastPracticeDate = lastPractice });
            await Harness.Context.SaveChangesAsync();
        }

        public async Task AddPanchangamAsync(DateOnly date, string rahu = "11:09 – 12:32")
        {
            Harness.Context.Panchangams.Add(new Panchangam
            {
                Date = date, Year = date.Year, RegionId = RegionId, DayOfWeek = date.DayOfWeek.ToString(),
                TithiDetails = "Vidiya upto 15:54, Tadiya from 15:55",
                NakshatramDetails = "Anuradha upto 11:38, Jyeshta from 11:39",
                RahuKalam = rahu, Yamagandam = "07:30 – 09:00", Gulika = "10:30 – 12:00",
                Durmuhurtham = "10:47 – 11:31", Varjyam = "17:52 – 19:38",
                AbhijitMuhurtham = "None", AmruthaKalam = "23:10 – 00:40, 12 Nov",
                Sunrise = new TimeOnly(6, 12), Sunset = new TimeOnly(17, 48), IsActive = true
            });
            await Harness.Context.SaveChangesAsync();
        }

        public async Task<NotificationConfig> ConfigAsync(string kind)
        {
            await Service.EnsureBuiltInTypesAsync();
            return await Harness.Context.NotificationConfigs.SingleAsync(c => c.Kind == kind);
        }

        public Task<EngineRunSummary> RunAsync(DateTime nowUtc, string? only = null, bool ignoreClock = false)
            => Engine.RunAsync(new EngineRunOptions(nowUtc, only, NotificationTriggers.Schedule, ignoreClock));

        public List<NotificationLog> Logs(string kind)
            => Harness.Context.NotificationLogs.AsNoTracking().Where(l => l.Kind == kind).OrderBy(l => l.CreatedDate).ToList();

        public void Dispose() => Harness.Dispose();
    }

    private sealed class FakeTransport : IPushTransport
    {
        public bool Ready { get; set; } = true;
        public HashSet<string> DeadTokens { get; } = new();
        public List<PushEnvelope> Sent { get; } = new();

        public PushTransportStatus Status => new(true, Ready, "test-project", Ready ? null : "No key.");

        public Task<IReadOnlyList<PushSendOutcome>> SendAsync(IReadOnlyList<PushEnvelope> envelopes, CancellationToken cancellationToken = default)
        {
            var outcomes = new List<PushSendOutcome>();
            foreach (var e in envelopes)
            {
                if (DeadTokens.Contains(e.Token))
                {
                    outcomes.Add(new PushSendOutcome(e.DeviceId, false, true, "Unregistered: the app was removed"));
                    continue;
                }

                Sent.Add(e);
                outcomes.Add(new PushSendOutcome(e.DeviceId, true, false, null));
            }

            return Task.FromResult<IReadOnlyList<PushSendOutcome>>(outcomes);
        }
    }

    /// <summary>Grants the Sanathan role every form unless a test takes one away.</summary>
    private sealed class FakeAccess : IAccessRightsCatalog
    {
        public HashSet<string> Denied { get; } = new();

        public Task<AccessSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlySet<string> granted = new[] { ModuleCodes.Sadhana, ModuleCodes.Panchangam, ModuleCodes.Calendar, ModuleCodes.MyNotifications }
                .Where(c => !Denied.Contains(c)).ToHashSet();
            return Task.FromResult(new AccessSnapshot(new Dictionary<string, IReadOnlySet<string>> { [RoleNames.Sanathan] = granted }));
        }

        public void Invalidate() { }
    }

    private sealed class EnglishOnlyCatalog : ITranslationCatalog
    {
        public Task<TranslationSnapshot?> GetAsync(string? languageCode, CancellationToken cancellationToken = default) => Task.FromResult<TranslationSnapshot?>(null);
        public void Invalidate() { }
    }

    // ------------------------------------------------------------------ sadhana reminder

    [Fact]
    public async Task The_sadhana_reminder_goes_out_at_the_configured_time_and_only_once()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();

        var first = await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);
        var second = await rig.RunAsync(Ist(6, 1), NotificationKinds.SadhanaReminder);

        Assert.True(first.Ran);
        Assert.Equal(1, first.Sent);
        Assert.Equal(0, second.Due);   // already raised for today: not due again

        var log = Assert.Single(rig.Logs(NotificationKinds.SadhanaReminder));
        Assert.Equal(seeker, log.UserId);
        Assert.Equal(NotificationStatuses.Sent, log.Status);
        Assert.Equal("/sadhana", log.Route);
        Assert.Contains("sadhana", log.Title, StringComparison.OrdinalIgnoreCase);

        var envelope = Assert.Single(rig.Transport.Sent);
        Assert.Equal(NotificationChannels.Sadhana, envelope.Message.Channel);
        Assert.Equal("/sadhana", envelope.Message.Route);
    }

    [Theory]
    [InlineData(5, 59, false)]   // not yet
    [InlineData(6, 0, true)]     // on the minute
    [InlineData(6, 29, true)]    // late, inside the grace period
    [InlineData(6, 30, false)]   // the moment has passed
    [InlineData(9, 0, false)]
    public async Task A_daily_notification_is_due_from_its_time_until_the_grace_period_ends(int hour, int minute, bool due)
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();

        var summary = await rig.RunAsync(Ist(hour, minute), NotificationKinds.SadhanaReminder);

        Assert.Equal(due ? 1 : 0, summary.Sent);
    }

    [Fact]
    public async Task The_seekers_own_time_replaces_the_configured_one()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        var config = await rig.ConfigAsync(NotificationKinds.SadhanaReminder);

        rig.Harness.Context.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = seeker, NotificationConfigId = config.Id, IsEnabled = true, FromTime = new TimeOnly(19, 15)
        });
        await rig.Harness.Context.SaveChangesAsync();

        Assert.Equal(0, (await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder)).Sent);
        Assert.Equal(1, (await rig.RunAsync(Ist(19, 15), NotificationKinds.SadhanaReminder)).Sent);
    }

    [Fact]
    public async Task A_seeker_who_already_practised_today_is_not_reminded()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        await rig.SetStreakAsync(seeker, 5, Today);

        var summary = await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        Assert.Equal(0, summary.Due);
        Assert.Empty(rig.Transport.Sent);
    }

    [Fact]
    public async Task A_live_streak_changes_the_reminders_wording()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        await rig.SetStreakAsync(seeker, 12, Today.AddDays(-1));

        await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        var log = Assert.Single(rig.Logs(NotificationKinds.SadhanaReminder));
        Assert.Contains("13", log.Body);          // "Day 13 of your streak…"
        Assert.DoesNotContain("{", log.Body);     // every placeholder was filled
    }

    [Fact]
    public async Task The_time_is_the_seekers_own_clock_not_indias()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync(timeZone: "America/New_York");

        // 06:00 in India is the previous evening in New York: nothing is due there.
        Assert.Equal(0, (await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder)).Sent);

        // 06:00 Eastern Standard Time on 11 Nov is 11:00 UTC.
        var sixInNewYork = new DateTime(2026, 11, 11, 11, 0, 0, DateTimeKind.Utc);
        Assert.Equal(1, (await rig.RunAsync(sixInNewYork, NotificationKinds.SadhanaReminder)).Sent);
    }

    // ------------------------------------------------------------------ streaks

    [Fact]
    public async Task A_streak_about_to_break_is_announced_in_the_evening()
    {
        using var rig = new Rig();
        var atRisk = await rig.AddSeekerAsync("Streak Holder");
        var practised = await rig.AddSeekerAsync("Already Sat");
        var beginner = await rig.AddSeekerAsync("One Day In");
        await rig.SetStreakAsync(atRisk, 9, Today.AddDays(-1));
        await rig.SetStreakAsync(practised, 9, Today);
        await rig.SetStreakAsync(beginner, 1, Today.AddDays(-1));   // below the threshold of 2

        var summary = await rig.RunAsync(Ist(20, 0), NotificationKinds.StreakAtRisk);

        Assert.Equal(1, summary.Sent);
        var log = Assert.Single(rig.Logs(NotificationKinds.StreakAtRisk));
        Assert.Equal(atRisk, log.UserId);
        Assert.Contains("9", log.Title);
    }

    [Fact]
    public async Task A_missed_streak_is_announced_once_the_morning_after()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        // Practised the day before yesterday, not yesterday: the streak broke yesterday.
        await rig.SetStreakAsync(seeker, 21, Today.AddDays(-2));

        var morning = await rig.RunAsync(Ist(8, 0), NotificationKinds.StreakMissed);
        var nextMorning = await rig.RunAsync(Ist(8, 0, Today.AddDays(1)), NotificationKinds.StreakMissed);

        Assert.Equal(1, morning.Sent);
        Assert.Equal(0, nextMorning.Due);   // a day later it is no longer "yesterday's" miss
        Assert.Contains("21", Assert.Single(rig.Logs(NotificationKinds.StreakMissed)).Title);
    }

    [Fact]
    public async Task The_streak_threshold_is_the_administrators_to_set()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        await rig.SetStreakAsync(seeker, 4, Today.AddDays(-1));

        var config = await rig.ConfigAsync(NotificationKinds.StreakAtRisk);
        config.Threshold = 7;
        await rig.Harness.Context.SaveChangesAsync();

        Assert.Equal(0, (await rig.RunAsync(Ist(20, 0), NotificationKinds.StreakAtRisk)).Due);
    }

    // ------------------------------------------------------------------ panchangam

    [Fact]
    public async Task The_morning_panchangam_names_the_tithi_the_nakshatram_and_rahu_kalam()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        await rig.AddPanchangamAsync(Today);

        await rig.RunAsync(Ist(6, 30), NotificationKinds.PanchangamDaily);

        var log = Assert.Single(rig.Logs(NotificationKinds.PanchangamDaily));
        Assert.Contains("Vidiya upto 15:54", log.Body);
        Assert.DoesNotContain("Tadiya", log.Body);   // only the tithi in force at sunrise
        Assert.Contains("Anuradha", log.Body);
        Assert.Contains("11:09", log.Body);
    }

    [Theory]
    [InlineData(10, 58, false)]   // eleven minutes before: outside the ten-minute lead
    [InlineData(10, 59, true)]    // ten minutes before
    [InlineData(11, 8, true)]     // a minute before
    [InlineData(11, 9, false)]    // it has begun: a warning now is too late
    public async Task Rahu_kalam_is_warned_of_inside_the_lead_time_only(int hour, int minute, bool warned)
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        await rig.AddPanchangamAsync(Today);

        var summary = await rig.RunAsync(Ist(hour, minute), NotificationKinds.RahuKalam);

        Assert.Equal(warned ? 1 : 0, summary.Sent);
        if (warned)
        {
            var log = Assert.Single(rig.Logs(NotificationKinds.RahuKalam));
            Assert.Contains("11:09", log.Title);
            Assert.Contains("12:32", log.Body);
        }
    }

    [Fact]
    public async Task One_window_is_warned_of_once_however_many_minutes_the_lead_spans()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        await rig.AddPanchangamAsync(Today);

        for (var minute = 59; minute <= 59 + 9; minute++)
            await rig.RunAsync(Ist(10, 0).AddMinutes(minute), NotificationKinds.RahuKalam);

        Assert.Single(rig.Logs(NotificationKinds.RahuKalam));
    }

    [Fact]
    public async Task A_window_warning_stops_being_worth_delivering_when_the_window_closes()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        await rig.AddPanchangamAsync(Today);

        await rig.RunAsync(Ist(11, 0), NotificationKinds.RahuKalam);

        // 11:00 to the window's end at 12:32.
        Assert.Equal(TimeSpan.FromMinutes(92), Assert.Single(rig.Transport.Sent).Message.Ttl);
    }

    [Fact]
    public async Task A_window_written_as_none_raises_nothing()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        await rig.AddPanchangamAsync(Today);   // AbhijitMuhurtham is "None" on a Wednesday

        var config = await rig.ConfigAsync(NotificationKinds.AbhijitMuhurtham);
        config.DefaultEnabledForUsers = true;
        await rig.Harness.Context.SaveChangesAsync();

        var summary = await rig.RunAsync(Ist(11, 40), NotificationKinds.AbhijitMuhurtham, ignoreClock: true);

        Assert.Equal(0, summary.Due);
    }

    // ------------------------------------------------------------------ festivals and calendar

    [Fact]
    public async Task A_festival_is_announced_on_its_morning_and_the_evening_before_in_its_region_only()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        var elsewhere = Guid.NewGuid();
        rig.Harness.Context.Festivals.AddRange(
            new Festival { Name = "Karthika Pournami", Year = 2026, Date = Today.AddDays(1), Regions = rig.RegionId.ToString(), IsActive = true },
            new Festival { Name = "Somewhere Else Utsavam", Year = 2026, Date = Today.AddDays(1), Regions = elsewhere.ToString(), IsActive = true });
        await rig.Harness.Context.SaveChangesAsync();

        await rig.RunAsync(Ist(18, 0), NotificationKinds.FestivalTomorrow);
        await rig.RunAsync(Ist(7, 0, Today.AddDays(1)), NotificationKinds.FestivalToday);

        var eve = Assert.Single(rig.Logs(NotificationKinds.FestivalTomorrow));
        var day = Assert.Single(rig.Logs(NotificationKinds.FestivalToday));
        Assert.Contains("Karthika Pournami", eve.Title);
        Assert.Contains("Karthika Pournami", day.Title);
        Assert.DoesNotContain("Somewhere Else", eve.Title);
    }

    [Fact]
    public async Task A_seekers_own_calendar_mark_is_announced_on_its_morning_yearly_repeats_included()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        rig.Harness.Context.CalendarEvents.AddRange(
            new CalendarEvent { UserId = seeker, Date = Today, Title = "Satyanarayana Vratam", Kind = CalendarEventKinds.Vratam, Time = new TimeOnly(9, 0) },
            new CalendarEvent { UserId = seeker, Date = new DateOnly(2020, 11, 11), Title = "Amma's Birthday", Kind = CalendarEventKinds.Birthday, RepeatYearly = true },
            new CalendarEvent { UserId = seeker, Date = Today.AddDays(3), Title = "Not Today", Kind = CalendarEventKinds.Personal });
        await rig.Harness.Context.SaveChangesAsync();

        await rig.RunAsync(Ist(7, 0), NotificationKinds.CalendarEvent);

        var logs = rig.Logs(NotificationKinds.CalendarEvent);
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.Title.Contains("Satyanarayana Vratam") && l.Body.Contains("09:00"));
        Assert.Contains(logs, l => l.Title.Contains("Amma's Birthday"));
    }

    // ------------------------------------------------------------------ who may be told

    [Fact]
    public async Task A_seeker_who_switched_a_type_off_is_left_alone()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        var config = await rig.ConfigAsync(NotificationKinds.SadhanaReminder);
        rig.Harness.Context.UserNotificationPreferences.Add(new UserNotificationPreference { UserId = seeker, NotificationConfigId = config.Id, IsEnabled = false });
        await rig.Harness.Context.SaveChangesAsync();

        Assert.Equal(0, (await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder)).Due);
    }

    [Fact]
    public async Task The_master_switch_silences_everything_but_a_mandatory_type()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        rig.Harness.Context.UserNotificationSettings.Add(new UserNotificationSetting { UserId = seeker, MasterEnabled = false });
        await rig.Harness.Context.SaveChangesAsync();

        Assert.Equal(0, (await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder)).Due);

        var config = await rig.ConfigAsync(NotificationKinds.SadhanaReminder);
        config.IsMandatory = true;
        await rig.Harness.Context.SaveChangesAsync();

        Assert.Equal(1, (await rig.RunAsync(Ist(6, 5), NotificationKinds.SadhanaReminder)).Sent);
    }

    [Fact]
    public async Task Quiet_hours_silence_even_a_mandatory_type()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        rig.Harness.Context.UserNotificationSettings.Add(new UserNotificationSetting
        {
            UserId = seeker, MasterEnabled = true, QuietHoursEnabled = true, QuietFrom = new TimeOnly(22, 0), QuietTo = new TimeOnly(7, 0)
        });
        var config = await rig.ConfigAsync(NotificationKinds.SadhanaReminder);
        config.IsMandatory = true;
        await rig.Harness.Context.SaveChangesAsync();

        Assert.Equal(0, (await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder)).Due);
    }

    [Fact]
    public async Task A_type_switched_off_by_the_administrator_is_not_evaluated()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        var config = await rig.ConfigAsync(NotificationKinds.SadhanaReminder);
        config.IsActive = false;
        await rig.Harness.Context.SaveChangesAsync();

        var summary = await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        Assert.False(summary.Ran);
        Assert.Empty(rig.Transport.Sent);
    }

    [Fact]
    public async Task A_role_denied_a_form_on_access_rights_gets_none_of_its_notifications()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        await rig.AddPanchangamAsync(Today);
        rig.Access.Denied.Add(ModuleCodes.Panchangam);

        Assert.Equal(0, (await rig.RunAsync(Ist(6, 30), NotificationKinds.PanchangamDaily)).Due);
        Assert.Equal(1, (await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder)).Sent);   // other forms unaffected
    }

    [Fact]
    public async Task A_seeker_with_no_phone_registered_is_not_in_the_audience()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        await rig.Store.RemoveDeviceAsync("token-" + seeker.ToString("N"));

        var summary = await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        Assert.False(summary.Ran);
        Assert.Contains("device", summary.SkippedReason, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ sending

    [Fact]
    public async Task Nothing_is_evaluated_or_written_while_push_is_not_configured()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();
        rig.Transport.Ready = false;

        var summary = await rig.RunAsync(Ist(6, 0));

        Assert.False(summary.Ran);
        Assert.Equal("No key.", summary.SkippedReason);
        Assert.Empty(rig.Harness.Context.NotificationLogs);
        Assert.Empty(rig.Harness.Context.NotificationRuns);
    }

    [Fact]
    public async Task A_token_the_push_service_disowns_is_retired_and_the_failure_is_recorded()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        rig.Transport.DeadTokens.Add("token-" + seeker.ToString("N"));

        var summary = await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        Assert.Equal(1, summary.Failed);
        var log = Assert.Single(rig.Logs(NotificationKinds.SadhanaReminder));
        Assert.Equal(NotificationStatuses.Failed, log.Status);
        Assert.Contains("Unregistered", log.Error);

        var device = await rig.Harness.Context.DeviceTokens.AsNoTracking().SingleAsync(d => d.UserId == seeker);
        Assert.False(device.IsActive);

        // And a failure is not retried the next minute: the occasion has been spent.
        Assert.False((await rig.RunAsync(Ist(6, 1), NotificationKinds.SadhanaReminder)).Ran);
    }

    [Fact]
    public async Task Every_phone_a_seeker_has_gets_the_message_and_it_is_logged_once()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        await rig.Store.UpsertDeviceAsync(seeker, 1, "second-phone", "Galaxy S24", "1.3.0", "Asia/Kolkata", "en");

        await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        Assert.Equal(2, rig.Transport.Sent.Count);
        Assert.Equal(2, Assert.Single(rig.Logs(NotificationKinds.SadhanaReminder)).Devices);
    }

    [Fact]
    public async Task A_pass_with_something_to_do_is_recorded_and_an_empty_one_is_not()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync();

        await rig.RunAsync(Ist(3, 0));    // nothing is due at three in the morning
        Assert.Empty(rig.Harness.Context.NotificationRuns);

        await rig.RunAsync(Ist(6, 0));
        var run = Assert.Single(rig.Harness.Context.NotificationRuns.AsNoTracking().ToList());
        Assert.Equal(NotificationKinds.SadhanaReminder, run.Kind);
        Assert.Equal(1, run.Sent);
    }

    [Fact]
    public async Task Run_now_reaches_only_seekers_who_have_not_had_todays()
    {
        using var rig = new Rig();
        await rig.AddSeekerAsync("Early Riser");
        await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);   // the first seeker gets theirs on time

        await rig.AddSeekerAsync("Signed Up Later");
        var manual = await rig.RunAsync(Ist(15, 0), NotificationKinds.SadhanaReminder, ignoreClock: true);

        Assert.Equal(1, manual.Sent);
        Assert.Equal(2, rig.Logs(NotificationKinds.SadhanaReminder).Count);
    }

    // ------------------------------------------------------------------ by hand

    [Fact]
    public async Task A_test_send_ignores_the_seekers_settings_and_fills_every_placeholder()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        rig.Harness.Context.UserNotificationSettings.Add(new UserNotificationSetting { UserId = seeker, MasterEnabled = false });
        await rig.Harness.Context.SaveChangesAsync();
        await rig.Service.EnsureBuiltInTypesAsync();

        foreach (var kind in NotificationKinds.All.Where(k => k.Schedule != NotificationSchedule.Manual))
        {
            var result = await rig.Engine.SendTestAsync(seeker, kind.Code);
            Assert.Equal(1, result.Sent);
        }

        Assert.All(rig.Transport.Sent, e =>
        {
            Assert.DoesNotContain("{", e.Message.Title + e.Message.Body);       // no unfilled placeholder
            Assert.DoesNotContain("push.", e.Message.Title + e.Message.Body);   // no missing label
        });
    }

    [Fact]
    public async Task A_broadcast_respects_the_announcements_switch_and_reports_who_has_no_phone()
    {
        using var rig = new Rig();
        var listening = await rig.AddSeekerAsync("Listening");
        var optedOut = await rig.AddSeekerAsync("Opted Out");
        var announcements = await rig.ConfigAsync(NotificationKinds.Announcement);
        rig.Harness.Context.UserNotificationPreferences.Add(new UserNotificationPreference { UserId = optedOut, NotificationConfigId = announcements.Id, IsEnabled = false });
        await rig.Harness.Context.SaveChangesAsync();

        var broadcast = await rig.Engine.SendDirectAsync(new DirectNotification(null, "Karthika Masam Begins", "New chants have been added."));
        Assert.Equal(1, broadcast.Sent);
        Assert.Equal(listening, Assert.Single(rig.Logs(NotificationKinds.Announcement)).UserId);

        var targeted = await rig.Engine.SendDirectAsync(new DirectNotification(new[] { SeedConstants.AdminUserId }, "Hello", "There", RespectPreferences: false));
        Assert.Equal(0, targeted.Sent);
        Assert.Equal(1, targeted.NoDevice);   // the seeded administrator has no phone here
    }

    // ------------------------------------------------------------------ inbox and devices

    [Fact]
    public async Task What_was_delivered_is_the_seekers_inbox_until_they_open_it()
    {
        using var rig = new Rig();
        var seeker = await rig.AddSeekerAsync();
        await rig.RunAsync(Ist(6, 0), NotificationKinds.SadhanaReminder);

        Assert.Equal(1, await rig.Store.CountUnreadAsync(seeker));
        Assert.Single(await rig.Store.GetInboxAsync(seeker, 30));

        await rig.Store.MarkInboxReadAsync(seeker, DateTime.UtcNow);

        Assert.Equal(0, await rig.Store.CountUnreadAsync(seeker));
        Assert.Single(await rig.Store.GetInboxAsync(seeker, 30));   // read, not gone
    }

    [Fact]
    public async Task A_phone_belongs_to_whoever_signed_in_on_it_last()
    {
        using var rig = new Rig();
        var first = await rig.AddSeekerAsync("First Owner");
        var second = await rig.AddSeekerAsync("Second Owner");

        await rig.Store.UpsertDeviceAsync(second, 1, "token-" + first.ToString("N"), "Pixel 8", "1.3.0", "Asia/Kolkata", "te");

        var audience = await rig.Store.GetAudienceAsync();
        Assert.DoesNotContain(audience, a => a.UserId == first);
        var owner = Assert.Single(audience, a => a.UserId == second);
        Assert.Equal(2, owner.Devices.Count);
        Assert.Equal("te", owner.Language);   // the phone used last speaks for the seeker
    }

    // ------------------------------------------------------------------ the catalogue

    [Fact]
    public async Task Built_in_types_are_created_once_on_the_form_they_belong_to()
    {
        using var rig = new Rig();

        var created = await rig.Service.EnsureBuiltInTypesAsync();
        var again = await rig.Service.EnsureBuiltInTypesAsync();

        Assert.Equal(NotificationKinds.All.Count, created);
        Assert.Equal(0, again);

        var rahu = await rig.Harness.Context.NotificationConfigs.Include(c => c.MenuModule).SingleAsync(c => c.Kind == NotificationKinds.RahuKalam);
        Assert.Equal(ModuleCodes.Panchangam, rahu.MenuModule!.Code);
        Assert.True(rahu.IsActive);
    }

    [Fact]
    public void Every_built_in_type_has_its_wording_in_the_shipped_english_file()
    {
        var english = LocalizationSeedFiles.Load()["en"];

        foreach (var kind in NotificationKinds.All)
        {
            Assert.True(english.ContainsKey($"push.{kind.Code}.name"), $"push.{kind.Code}.name is missing");
            Assert.True(english.ContainsKey($"push.{kind.Code}.desc"), $"push.{kind.Code}.desc is missing");
            if (kind.Schedule == NotificationSchedule.Manual) continue;

            Assert.True(english.ContainsKey(kind.TitleKey), $"{kind.TitleKey} is missing");
            Assert.True(english.ContainsKey(kind.BodyKey), $"{kind.BodyKey} is missing");
        }

        // The English name on the settings screens is the catalogue's; the two must not drift apart.
        Assert.All(NotificationKinds.All, k => Assert.Equal(k.Name, english[$"push.{k.Code}.name"]));
    }
}

public class PanchangamWindowTextTests
{
    private static readonly DateOnly Row = new(2026, 11, 11);

    [Fact]
    public void A_plain_window_is_read_on_the_rows_date()
    {
        var span = Assert.Single(PanchangamWindowText.Parse("11:09 – 12:32", Row));
        Assert.Equal(new DateTime(2026, 11, 11, 11, 9, 0), span.Start);
        Assert.Equal(new DateTime(2026, 11, 11, 12, 32, 0), span.End);
    }

    [Fact]
    public void Several_windows_in_one_field_are_all_read()
    {
        var spans = PanchangamWindowText.Parse("08:40 – 09:24, 12:20 – 13:04", Row);
        Assert.Equal(2, spans.Count);
        Assert.Equal(new TimeOnly(12, 20), TimeOnly.FromDateTime(spans[1].Start));
    }

    [Fact]
    public void A_day_suffix_moves_that_instant_to_the_later_day()
    {
        var span = Assert.Single(PanchangamWindowText.Parse("23:10 – 00:40, 12 Nov", Row));
        Assert.Equal(new DateTime(2026, 11, 11, 23, 10, 0), span.Start);
        Assert.Equal(new DateTime(2026, 11, 12, 0, 40, 0), span.End);
    }

    [Fact]
    public void A_window_wholly_after_midnight_is_read_on_the_next_day()
    {
        var span = Assert.Single(PanchangamWindowText.Parse("01:15, 12 Nov – 02:50, 12 Nov", Row));
        Assert.Equal(new DateTime(2026, 11, 12, 1, 15, 0), span.Start);
        Assert.Equal(new DateTime(2026, 11, 12, 2, 50, 0), span.End);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("None")]
    [InlineData("లేదు")]
    public void Nothing_is_read_from_an_empty_field_or_none_in_any_language(string? text)
        => Assert.Empty(PanchangamWindowText.Parse(text, Row));

    [Theory]
    [InlineData("Vidiya upto 15:54, Tadiya from 15:55", "Vidiya upto 15:54")]
    [InlineData("Anuradha upto 01:20, 12 Nov, Jyeshta from 01:21, 12 Nov", "Anuradha upto 01:20, 12 Nov")]
    [InlineData("Pournami full day", "Pournami full day")]
    [InlineData(null, "")]
    public void The_first_segment_is_the_one_in_force_at_sunrise(string? text, string expected)
        => Assert.Equal(expected, PanchangamWindowText.FirstSegment(text));
}

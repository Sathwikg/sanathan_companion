using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using PanchangamDay = Sanathana.Companion.Domain.Entities.Panchangam;

namespace Sanathana.Companion.Application.Notifications;

/// <param name="NowUtc">The instant to evaluate at; the real clock when null. Tests pin it.</param>
/// <param name="OnlyKind">Evaluate just this type (an administrator's "run now").</param>
/// <param name="Trigger">One of <see cref="NotificationTriggers"/>, recorded on every row raised.</param>
/// <param name="IgnoreClock">
/// Treat every type as due now. Everything else still applies — the type's own condition, the
/// seeker's switches and quiet hours, and above all the dedupe key, so a "run now" reaches only the
/// seekers who have not already had today's.
/// </param>
public sealed record EngineRunOptions(
    DateTime? NowUtc = null,
    string? OnlyKind = null,
    string Trigger = NotificationTriggers.Schedule,
    bool IgnoreClock = false);

public sealed record KindRunResult(string Kind, int Due, int Sent, int Failed, string? Error);

public sealed class EngineRunSummary
{
    /// <summary>False when the run did not look at any type; <see cref="SkippedReason"/> says why.</summary>
    public bool Ran { get; init; }
    public string? SkippedReason { get; init; }

    /// <summary>Seekers with a reachable device at the time of the run.</summary>
    public int Audience { get; init; }

    public List<KindRunResult> Kinds { get; } = new();

    public int Due => Kinds.Sum(k => k.Due);
    public int Sent => Kinds.Sum(k => k.Sent);
    public int Failed => Kinds.Sum(k => k.Failed);
}

/// <summary>A message an administrator wrote, to chosen seekers or to everyone.</summary>
/// <param name="UserIds">Null addresses every reachable seeker.</param>
/// <param name="RespectPreferences">Honour each seeker's Announcements switch and quiet hours. A test to oneself does not.</param>
public sealed record DirectNotification(
    IReadOnlyCollection<Guid>? UserIds,
    string Title,
    string Body,
    string? Route = null,
    string? ImageUrl = null,
    bool RespectPreferences = true,
    string Trigger = NotificationTriggers.Broadcast);

/// <param name="Recipients">Seekers the message was addressed to after preferences were applied.</param>
/// <param name="NoDevice">Seekers named in the request who have no phone registered.</param>
public sealed record DeliverySummary(int Recipients, int Sent, int Failed, int NoDevice, IReadOnlyList<string> Errors);

public interface INotificationEngine
{
    /// <summary>One pass over every active notification type. The recurring job calls this each minute.</summary>
    Task<EngineRunSummary> RunAsync(EngineRunOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends one seeker a sample of a type, exactly as it would look, whatever their settings say.</summary>
    Task<DeliverySummary> SendTestAsync(Guid userId, string kindCode, CancellationToken cancellationToken = default);

    /// <summary>Sends an administrator's own message.</summary>
    Task<DeliverySummary> SendDirectAsync(DirectNotification message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Decides who is due which notification right now, and sends each one exactly once.
/// </summary>
/// <remarks>
/// The engine holds no schedule of its own. Every minute it asks, for each active type, "who is due
/// this at this instant?" and answers from the data: the seeker's chosen time, their streak, their
/// region's Panchangam. That makes it restart-proof (nothing queued is lost, because nothing is
/// queued) and makes a change to a seeker's settings or an administrator's config take effect on the
/// next minute without anything being rescheduled.
/// <para>
/// "Exactly once" is the dedupe key. Each notification has a key naming the type, the seeker and the
/// occasion; the row carrying it is written before the send and the column is unique, so a second
/// run, a second server or a retry after a crash cannot raise the same one again. A type stays due
/// for <see cref="PushNotificationOptions.GraceMinutes"/> after its moment, so a run that was late
/// or a server that was restarting still delivers; past that the moment is let go, because a
/// six o'clock reminder at nine is noise.
/// </para>
/// <para>
/// Clock times are the seeker's own (their phone reports its time zone). Dates of practice and of
/// the Panchangam are India's, as everywhere else on the server.
/// </para>
/// </remarks>
public sealed class NotificationEngine : INotificationEngine
{
    private const int ChunkSize = 100;

    /// <summary>How long a notice about "now" (a reminder, a streak) is worth delivering to a phone that was unreachable.</summary>
    private static readonly TimeSpan MomentTtl = TimeSpan.FromHours(4);
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    private readonly INotificationStore _store;
    private readonly IPushTransport _transport;
    private readonly IPushTextProvider _texts;
    private readonly IAccessRightsCatalog _access;
    private readonly INotificationService _catalog;
    private readonly PushNotificationOptions _options;
    private readonly ILogger<NotificationEngine> _logger;

    public NotificationEngine(
        INotificationStore store,
        IPushTransport transport,
        IPushTextProvider texts,
        IAccessRightsCatalog access,
        INotificationService catalog,
        IOptions<PushNotificationOptions> options,
        ILogger<NotificationEngine> logger)
    {
        _store = store;
        _transport = transport;
        _texts = texts;
        _access = access;
        _catalog = catalog;
        _options = options.Value;
        _logger = logger;
    }

    // ------------------------------------------------------------------ the scheduled pass

    public async Task<EngineRunSummary> RunAsync(EngineRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new EngineRunOptions();

        var status = _transport.Status;
        if (!status.Ready)
            return new EngineRunSummary { Ran = false, SkippedReason = status.Reason ?? "Push notifications are not configured." };

        var configs = await ActiveConfigsAsync(cancellationToken);
        var scheduled = configs
            .Select(c => (Config: c, Kind: NotificationKinds.Find(c.Kind)))
            .Where(x => x.Kind is not null
                     && x.Kind.Schedule is NotificationSchedule.UserTime or NotificationSchedule.DailyAt or NotificationSchedule.BeforeWindow
                     && (options.OnlyKind is null || x.Kind.Code == options.OnlyKind))
            .ToList();

        if (scheduled.Count == 0)
            return new EngineRunSummary { Ran = false, SkippedReason = "No notification type is switched on." };

        var audience = await _store.GetAudienceAsync(null, cancellationToken);
        if (audience.Count == 0)
            return new EngineRunSummary { Ran = false, SkippedReason = "No seeker has a device registered." };

        var summary = new EngineRunSummary { Ran = true, Audience = audience.Count };
        var context = new RunContext(options.NowUtc ?? DateTime.UtcNow, audience, options.IgnoreClock,
            TimeSpan.FromMinutes(Math.Max(1, _options.GraceMinutes)), await _access.GetAsync(cancellationToken));

        foreach (var (config, kind) in scheduled)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clock = Stopwatch.StartNew();
            var due = 0;
            var sent = 0;
            var failed = 0;
            string? error = null;

            try
            {
                var candidates = await EvaluateAsync(config, kind!, context, cancellationToken);
                (due, sent, failed) = await DeliverAsync(candidates, options.Trigger, context.NowUtc, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One type failing (a malformed Panchangam row, say) must not silence the others.
                error = ex.Message;
                _logger.LogError(ex, "Notification type {Kind} failed", kind!.Code);
            }

            summary.Kinds.Add(new KindRunResult(kind!.Code, due, sent, failed, error));

            // Empty passes are not recorded: see NotificationRun. A manual run always is, so the
            // administrator who pressed the button can see that it ran and found nobody due.
            if (due > 0 || error is not null || options.Trigger != NotificationTriggers.Schedule)
            {
                await _store.AddRunAsync(new NotificationRun
                {
                    Kind = kind.Code,
                    StartedAtUtc = context.NowUtc,
                    DurationMs = (int)clock.ElapsedMilliseconds,
                    Due = due,
                    Sent = sent,
                    Failed = failed,
                    Trigger = options.Trigger,
                    Error = Truncate(error, 1000)
                }, cancellationToken);
            }
        }

        if (summary.Due > 0)
            _logger.LogInformation("Notification run: {Due} due, {Sent} sent, {Failed} failed across {Audience} seekers",
                summary.Due, summary.Sent, summary.Failed, summary.Audience);

        return summary;
    }

    /// <summary>The switched-on types whose form is still active, creating the built-in ones a new deployment lacks.</summary>
    private async Task<List<NotificationConfig>> ActiveConfigsAsync(CancellationToken cancellationToken)
    {
        var configs = await _store.GetConfigsAsync(cancellationToken);

        var present = configs.Select(c => c.Kind).ToHashSet(StringComparer.Ordinal);
        if (NotificationKinds.All.Any(k => !present.Contains(k.Code)))
        {
            if (await _catalog.EnsureBuiltInTypesAsync(cancellationToken) > 0)
                configs = await _store.GetConfigsAsync(cancellationToken);
        }

        return configs.Where(c => c.IsActive && (c.MenuModule?.IsActive ?? false)).ToList();
    }

    // ------------------------------------------------------------------ the rules

    private sealed record Candidate(
        AudienceMember User,
        NotificationConfig? Config,
        NotificationKind Kind,
        string DedupeKey,
        string Title,
        string Body,
        string? Route,
        string? ImageUrl,
        TimeSpan? Ttl = null);

    private Task<List<Candidate>> EvaluateAsync(NotificationConfig config, NotificationKind kind, RunContext context, CancellationToken cancellationToken)
        => kind.Code switch
        {
            NotificationKinds.SadhanaReminder => SadhanaReminderAsync(config, kind, context, cancellationToken),
            NotificationKinds.StreakAtRisk => StreakAsync(config, kind, context, atRisk: true, cancellationToken),
            NotificationKinds.StreakMissed => StreakAsync(config, kind, context, atRisk: false, cancellationToken),
            NotificationKinds.PanchangamDaily => PanchangamDailyAsync(config, kind, context, cancellationToken),
            NotificationKinds.FestivalToday => FestivalAsync(config, kind, context, daysAhead: 0, cancellationToken),
            NotificationKinds.FestivalTomorrow => FestivalAsync(config, kind, context, daysAhead: 1, cancellationToken),
            NotificationKinds.CalendarEvent => CalendarEventAsync(config, kind, context, cancellationToken),
            _ when kind.Schedule == NotificationSchedule.BeforeWindow => WindowAsync(config, kind, context, cancellationToken),
            _ => Task.FromResult(new List<Candidate>())
        };

    /// <summary>"Time for your japa", at the seeker's own hour, on days they have not practised yet.</summary>
    private async Task<List<Candidate>> SadhanaReminderAsync(NotificationConfig config, NotificationKind kind, RunContext context, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        var fallbackTime = config.SendTime ?? kind.SendTime ?? new TimeOnly(6, 0);
        var streaks = await context.StreaksAsync(_store, cancellationToken);

        foreach (var user in context.Audience)
        {
            if (!Reachable(user, config, kind, context, honourWindow: false)) continue;

            // For a type the seeker times themselves, the start of their window IS the time.
            var time = user.Preferences.GetValueOrDefault(config.Id)?.FromTime ?? fallbackTime;
            if (context.DueDate(user, time) is not { } date) continue;

            streaks.TryGetValue(user.UserId, out var streak);
            if (streak?.LastPracticeDate == context.TodayIst) continue;   // already sat today

            var texts = await context.TextsAsync(_texts, user.Language, cancellationToken);
            var alive = streak is { CurrentStreak: > 0 } && streak.LastPracticeDate == context.TodayIst.AddDays(-1);
            var args = Args(user, ("streak", (streak?.CurrentStreak ?? 0).ToString()), ("next", ((streak?.CurrentStreak ?? 0) + 1).ToString()));

            result.Add(new Candidate(user, config, kind,
                $"{kind.Code}|{user.UserId:N}|{date:yyyyMMdd}",
                texts.Format(kind.TitleKey, args),
                texts.Format(alive ? $"push.{kind.Code}.bodyStreak" : kind.BodyKey, args),
                kind.Route, config.ImageUrl, MomentTtl));
        }

        return result;
    }

    /// <summary>
    /// The two streak notices. At risk: a streak is alive and today has not been practised, said in
    /// the evening while there is still time. Missed: yesterday went by without practice and the
    /// streak that broke was worth mentioning, said once, the morning after.
    /// </summary>
    private async Task<List<Candidate>> StreakAsync(NotificationConfig config, NotificationKind kind, RunContext context, bool atRisk, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        var time = config.SendTime ?? kind.SendTime ?? new TimeOnly(20, 0);
        var threshold = Math.Max(1, config.Threshold ?? kind.Threshold ?? 1);
        var streaks = await context.StreaksAsync(_store, cancellationToken);

        // The streak row is only rewritten when the seeker next practises, so on the morning after
        // a miss CurrentStreak still holds the length of the streak that just ended.
        var lastPractice = context.TodayIst.AddDays(atRisk ? -1 : -2);

        foreach (var user in context.Audience)
        {
            if (!streaks.TryGetValue(user.UserId, out var streak)) continue;
            if (streak.LastPracticeDate != lastPractice || streak.CurrentStreak < threshold) continue;
            if (!Reachable(user, config, kind, context, honourWindow: true)) continue;
            if (context.DueDate(user, time) is not { } date) continue;

            var texts = await context.TextsAsync(_texts, user.Language, cancellationToken);
            var args = Args(user, ("streak", streak.CurrentStreak.ToString()), ("longest", streak.LongestStreak.ToString()));

            // Keyed on the day last practised for "missed": one broken streak, one notice, however
            // the clock and the grace period fall. Keyed on the date for "at risk": one per evening.
            var occasion = atRisk ? date.ToString("yyyyMMdd") : lastPractice.ToString("yyyyMMdd");

            result.Add(new Candidate(user, config, kind,
                $"{kind.Code}|{user.UserId:N}|{occasion}",
                texts.Format(kind.TitleKey, args),
                texts.Format(kind.BodyKey, args),
                kind.Route, config.ImageUrl, MomentTtl));
        }

        return result;
    }

    /// <summary>The morning summary: what day it is by the Panchangam, and when to be careful.</summary>
    private async Task<List<Candidate>> PanchangamDailyAsync(NotificationConfig config, NotificationKind kind, RunContext context, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        var time = config.SendTime ?? kind.SendTime ?? new TimeOnly(6, 30);
        var rows = await context.PanchangamAsync(_store, cancellationToken);

        foreach (var user in context.Audience)
        {
            if (!Reachable(user, config, kind, context, honourWindow: true)) continue;
            if (context.DueDate(user, time) is not { } date) continue;
            if (!rows.TryGetValue((context.RegionOf(user), date), out var row)) continue;

            var texts = await context.TextsAsync(_texts, user.Language, cancellationToken);
            var args = PanchangamArgs(user, row, texts);

            result.Add(new Candidate(user, config, kind,
                $"{kind.Code}|{user.UserId:N}|{date:yyyyMMdd}",
                texts.Format(kind.TitleKey, args),
                texts.Format(kind.BodyKey, args),
                kind.Route, config.ImageUrl));
        }

        return result;
    }

    /// <summary>
    /// "Rahu Kalam begins at 10:30" and its six siblings: a note a configured number of minutes
    /// before one of the day's windows opens in the seeker's region.
    /// </summary>
    private async Task<List<Candidate>> WindowAsync(NotificationConfig config, NotificationKind kind, RunContext context, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        if (kind.Window is null) return result;

        var lead = TimeSpan.FromMinutes(Math.Clamp(config.LeadMinutes ?? kind.LeadMinutes ?? 10, 1, 180));
        var rows = await context.PanchangamAsync(_store, cancellationToken);
        var nowIst = context.NowIst;

        // The next opening per region, once, not per seeker. A window that spills past midnight is
        // written in the previous day's row, so both days are read.
        var next = new Dictionary<Guid, PanchangamWindowSpan>();
        foreach (var regionId in context.Audience.Select(context.RegionOf).Distinct())
        {
            var spans = new List<PanchangamWindowSpan>();
            foreach (var date in new[] { context.TodayIst.AddDays(-1), context.TodayIst })
                if (rows.TryGetValue((regionId, date), out var row))
                    spans.AddRange(PanchangamWindowText.Parse(kind.Window(row), row.Date));

            var upcoming = spans.Where(s => s.Start > nowIst).OrderBy(s => s.Start).ToList();
            if (upcoming.Count == 0) continue;

            // On the schedule: only an opening inside the lead time. On "run now": the day's next one.
            var span = upcoming[0];
            if (context.IgnoreClock ? span.Start - nowIst <= TimeSpan.FromHours(24) : span.Start - nowIst <= lead)
                next[regionId] = span;
        }

        if (next.Count == 0) return result;

        foreach (var user in context.Audience)
        {
            if (!next.TryGetValue(context.RegionOf(user), out var span)) continue;
            if (!Reachable(user, config, kind, context, honourWindow: true)) continue;

            var texts = await context.TextsAsync(_texts, user.Language, cancellationToken);
            var minutes = Math.Max(1, (int)Math.Ceiling((span.Start - nowIst).TotalMinutes));
            var args = Args(user,
                ("start", span.Start.ToString("HH:mm")),
                ("end", span.End.ToString("HH:mm")),
                ("minutes", minutes.ToString()));

            result.Add(new Candidate(user, config, kind,
                $"{kind.Code}|{user.UserId:N}|{span.Start:yyyyMMddHHmm}",
                texts.Format(kind.TitleKey, args),
                texts.Format(kind.BodyKey, args),
                kind.Route, config.ImageUrl,
                // Worth delivering until the window closes, and not a minute after.
                Ttl: span.End - nowIst));
        }

        return result;
    }

    /// <summary>A festival observed in the seeker's region, on its morning or the evening before.</summary>
    private async Task<List<Candidate>> FestivalAsync(NotificationConfig config, NotificationKind kind, RunContext context, int daysAhead, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        var time = config.SendTime ?? kind.SendTime ?? new TimeOnly(7, 0);
        var festivals = await context.FestivalsAsync(_store, cancellationToken);
        if (festivals.Count == 0) return result;

        foreach (var user in context.Audience)
        {
            if (!Reachable(user, config, kind, context, honourWindow: true)) continue;
            if (context.DueDate(user, time) is not { } date) continue;

            var day = date.AddDays(daysAhead);
            var regionId = user.RegionId;
            var names = festivals
                .Where(f => f.Date == day && RegionFilter.FestivalInRegion(f, regionId))
                .Select(f => f.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (names.Count == 0) continue;

            var texts = await context.TextsAsync(_texts, user.Language, cancellationToken);
            var args = Args(user, ("festival", string.Join(", ", names.Select(texts.Term))));

            result.Add(new Candidate(user, config, kind,
                $"{kind.Code}|{user.UserId:N}|{day:yyyyMMdd}",
                texts.Format(kind.TitleKey, args),
                texts.Format(kind.BodyKey, args),
                kind.Route, config.ImageUrl));
        }

        return result;
    }

    /// <summary>The seeker's own calendar marks, on the morning of each.</summary>
    private async Task<List<Candidate>> CalendarEventAsync(NotificationConfig config, NotificationKind kind, RunContext context, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        var time = config.SendTime ?? kind.SendTime ?? new TimeOnly(7, 0);

        var due = new List<(AudienceMember User, DateOnly Date)>();
        foreach (var user in context.Audience)
        {
            if (!Reachable(user, config, kind, context, honourWindow: true)) continue;
            if (context.DueDate(user, time) is { } date) due.Add((user, date));
        }

        foreach (var group in due.GroupBy(d => d.Date))
        {
            var users = group.ToDictionary(g => g.User.UserId, g => g.User);
            var events = await _store.GetCalendarEventsAsync(users.Keys, group.Key, cancellationToken);

            foreach (var mark in events)
            {
                if (!users.TryGetValue(mark.UserId, out var user)) continue;

                var texts = await context.TextsAsync(_texts, user.Language, cancellationToken);
                var args = Args(user,
                    ("title", mark.Title),
                    ("kind", texts.Term(mark.Kind)),
                    ("time", mark.Time?.ToString("HH:mm") ?? string.Empty));

                result.Add(new Candidate(user, config, kind,
                    $"{kind.Code}|{mark.Id:N}|{group.Key:yyyyMMdd}",
                    texts.Format(kind.TitleKey, args),
                    texts.Format(mark.Time is null ? kind.BodyKey : $"push.{kind.Code}.bodyTime", args),
                    kind.Route, config.ImageUrl));
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ who may be told

    /// <summary>
    /// The one place that decides whether a seeker may receive a type right now. Mirrors
    /// NotificationService.Resolve, which answers the same question for the settings screen.
    /// </summary>
    private static bool Reachable(AudienceMember user, NotificationConfig config, NotificationKind kind, RunContext context, bool honourWindow)
    {
        var preference = user.Preferences.GetValueOrDefault(config.Id);

        // Mandatory types survive an opt-out and the master switch, but never quiet hours.
        var wanted = config.IsMandatory || (preference?.IsEnabled ?? config.DefaultEnabledForUsers);
        if (!wanted) return false;
        if (!user.MasterEnabled && !config.IsMandatory) return false;

        var localTime = TimeOnly.FromDateTime(context.LocalNow(user));
        if (user.QuietHoursEnabled && TimeWindow.Contains(user.QuietFrom, user.QuietTo, localTime)) return false;

        if (honourWindow && preference is { FromTime: not null, ToTime: not null }
            && !TimeWindow.Contains(preference.FromTime, preference.ToTime, localTime))
            return false;

        // A form withheld from a role on Access Rights does not notify that role either.
        return user.RoleName == RoleNames.Admin || context.Access.Allows(user.RoleName, new[] { kind.ModuleCode });
    }

    private static Dictionary<string, string> Args(AudienceMember user, params (string Name, string Value)[] more)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = user.Name };
        foreach (var (name, value) in more) args[name] = value;
        return args;
    }

    private static Dictionary<string, string> PanchangamArgs(AudienceMember user, PanchangamDay row, PushTexts texts)
        => Args(user,
            ("tithi", texts.Phrase(PanchangamWindowText.FirstSegment(row.TithiDetails))),
            ("nakshatram", texts.Phrase(PanchangamWindowText.FirstSegment(row.NakshatramDetails))),
            ("rahu", texts.Phrase(row.RahuKalam)),
            ("sunrise", row.Sunrise?.ToString("HH:mm") ?? string.Empty),
            ("sunset", row.Sunset?.ToString("HH:mm") ?? string.Empty));

    // ------------------------------------------------------------------ sending

    /// <summary>
    /// Claims, sends and records. Returns how many were due once already-sent ones were set aside,
    /// and how those fared.
    /// </summary>
    private async Task<(int Due, int Sent, int Failed)> DeliverAsync(List<Candidate> candidates, string trigger, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return (0, 0, 0);

        // Two rules can produce the same key in one pass only by mistake; never send it twice.
        candidates = candidates.GroupBy(c => c.DedupeKey, StringComparer.Ordinal).Select(g => g.First()).ToList();

        var existing = await _store.GetExistingKeysAsync(candidates.Select(c => c.DedupeKey).ToList(), cancellationToken);
        candidates = candidates.Where(c => !existing.Contains(c.DedupeKey)).ToList();
        if (candidates.Count == 0) return (0, 0, 0);

        var sent = 0;
        var failed = 0;

        foreach (var chunk in candidates.Chunk(ChunkSize))
        {
            var byKey = chunk.ToDictionary(c => c.DedupeKey, StringComparer.Ordinal);
            var logs = chunk.Select(c => new NotificationLog
            {
                UserId = c.User.UserId,
                Kind = c.Kind.Code,
                NotificationConfigId = c.Config?.Id,
                Title = Truncate(c.Title, NotificationLog.MaxTitleLength)!,
                Body = Truncate(c.Body, NotificationLog.MaxBodyLength)!,
                Route = c.Route,
                DedupeKey = c.DedupeKey,
                Status = NotificationStatuses.Pending,
                Trigger = trigger
            }).ToList();

            // Written before anything is sent: a row another run already holds is dropped here.
            var claimed = await _store.ClaimAsync(logs, cancellationToken);
            if (claimed.Count == 0) continue;

            var envelopes = new List<PushEnvelope>();
            var owner = new Dictionary<Guid, NotificationLog>();
            foreach (var log in claimed)
            {
                var candidate = byKey[log.DedupeKey];
                var message = new PushMessage(log.Title, log.Body, candidate.Kind.Code, candidate.Kind.Channel,
                    candidate.Route, candidate.ImageUrl,
                    // Same type, same seeker: the newer one replaces the older in the shade.
                    Tag: candidate.Kind.Code,
                    Ttl: candidate.Ttl);

                foreach (var device in candidate.User.Devices)
                {
                    envelopes.Add(new PushEnvelope(device.Id, device.Token, device.Platform, message));
                    owner[device.Id] = log;
                }
            }

            IReadOnlyList<PushSendOutcome> outcomes;
            try
            {
                outcomes = await _transport.SendAsync(envelopes, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Push transport failed for a batch of {Count}", envelopes.Count);
                outcomes = envelopes.Select(e => new PushSendOutcome(e.DeviceId, false, false, ex.Message)).ToList();
            }

            foreach (var group in outcomes.GroupBy(o => owner[o.DeviceId]))
            {
                var log = group.Key;
                log.Devices = group.Count(o => o.Ok);
                if (log.Devices > 0)
                {
                    log.Status = NotificationStatuses.Sent;
                    log.SentAtUtc = nowUtc;
                    sent++;
                }
                else
                {
                    log.Status = NotificationStatuses.Failed;
                    log.Error = Truncate(group.Select(o => o.Error).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)) ?? "No device accepted the message.", 1000);
                    failed++;
                }
            }

            await _store.CompleteAsync(claimed,
                outcomes.Select(o => new DeviceOutcome(o.DeviceId, o.Ok, o.TokenDead, o.Error)).ToList(),
                nowUtc, cancellationToken);
        }

        return (sent + failed, sent, failed);
    }

    // ------------------------------------------------------------------ by hand

    public async Task<DeliverySummary> SendTestAsync(Guid userId, string kindCode, CancellationToken cancellationToken = default)
    {
        var kind = NotificationKinds.Find(kindCode);
        if (kind is null) return new DeliverySummary(0, 0, 0, 0, new[] { $"Unknown notification type '{kindCode}'." });
        if (NotReady() is { } notReady) return notReady;

        var audience = await _store.GetAudienceAsync(new[] { userId }, cancellationToken);
        if (audience.Count == 0) return new DeliverySummary(0, 0, 0, 1, Array.Empty<string>());

        var user = audience[0];
        var nowUtc = DateTime.UtcNow;
        var config = (await _store.GetConfigsAsync(cancellationToken)).FirstOrDefault(c => c.Kind == kind.Code);
        var texts = await _texts.GetAsync(user.Language, cancellationToken);

        // Real figures where there are some, so the sample reads like the notification will; made-up
        // ones otherwise, so every placeholder is filled and a missing one is obvious.
        var args = Args(user,
            ("streak", "7"), ("next", "8"), ("longest", "21"),
            ("tithi", texts.Phrase("Shukla Dashami upto 14:05")), ("nakshatram", texts.Phrase("Rohini upto 18:40")),
            ("rahu", "10:30 – 12:00"), ("sunrise", "06:12"), ("sunset", "17:48"),
            ("start", "10:30"), ("end", "12:00"), ("minutes", (config?.LeadMinutes ?? kind.LeadMinutes ?? 10).ToString()),
            ("festival", "Maha Shivaratri"), ("title", "Satyanarayana Vratam"), ("kind", texts.Term(CalendarEventKinds.Vratam)), ("time", "09:00"));

        var todayIst = DateOnly.FromDateTime(nowUtc + IstOffset);
        var regionId = user.RegionId ?? await _store.GetFallbackRegionIdAsync(cancellationToken);
        if (regionId is { } region)
        {
            var row = (await _store.GetPanchangamAsync(new[] { region }, todayIst, todayIst, cancellationToken)).FirstOrDefault();
            if (row is not null)
            {
                foreach (var (name, value) in PanchangamArgs(user, row, texts))
                    if (!string.IsNullOrWhiteSpace(value)) args[name] = value;

                if (kind.Window is not null && PanchangamWindowText.Parse(kind.Window(row), row.Date).FirstOrDefault() is { } span && span.End > span.Start)
                {
                    args["start"] = span.Start.ToString("HH:mm");
                    args["end"] = span.End.ToString("HH:mm");
                }
            }
        }

        var candidate = new Candidate(user, config, kind,
            $"test|{kind.Code}|{user.UserId:N}|{nowUtc.Ticks}",
            texts.Format(kind.TitleKey, args),
            texts.Format(kind.BodyKey, args),
            kind.Route, config?.ImageUrl);

        var (_, sent, failed) = await DeliverAsync(new List<Candidate> { candidate }, NotificationTriggers.Test, nowUtc, cancellationToken);
        return new DeliverySummary(1, sent, failed, 0, Array.Empty<string>());
    }

    public async Task<DeliverySummary> SendDirectAsync(DirectNotification message, CancellationToken cancellationToken = default)
    {
        if (NotReady() is { } notReady) return notReady;

        var audience = await _store.GetAudienceAsync(message.UserIds, cancellationToken);
        var noDevice = message.UserIds is null ? 0 : message.UserIds.Distinct().Count() - audience.Count;

        var kind = NotificationKinds.Find(NotificationKinds.Announcement)!;
        var config = (await _store.GetConfigsAsync(cancellationToken)).FirstOrDefault(c => c.Kind == kind.Code);
        var nowUtc = DateTime.UtcNow;
        var batch = Guid.NewGuid().ToString("N");

        var candidates = new List<Candidate>();
        foreach (var user in audience)
        {
            if (message.RespectPreferences && config is not null)
            {
                var preference = user.Preferences.GetValueOrDefault(config.Id);
                var wanted = config.IsMandatory || (preference?.IsEnabled ?? config.DefaultEnabledForUsers);
                if (!wanted || (!user.MasterEnabled && !config.IsMandatory)) continue;

                var localTime = TimeOnly.FromDateTime(RunContext.ToLocal(nowUtc, user.TimeZoneId));
                if (user.QuietHoursEnabled && TimeWindow.Contains(user.QuietFrom, user.QuietTo, localTime)) continue;
            }

            candidates.Add(new Candidate(user, config, kind,
                $"direct|{batch}|{user.UserId:N}",
                message.Title.Trim(), message.Body.Trim(),
                string.IsNullOrWhiteSpace(message.Route) ? kind.Route : message.Route.Trim(),
                string.IsNullOrWhiteSpace(message.ImageUrl) ? null : message.ImageUrl.Trim()));
        }

        var (due, sent, failed) = await DeliverAsync(candidates, message.Trigger, nowUtc, cancellationToken);
        return new DeliverySummary(due, sent, failed, Math.Max(0, noDevice), Array.Empty<string>());
    }

    private DeliverySummary? NotReady()
    {
        var status = _transport.Status;
        return status.Ready ? null : new DeliverySummary(0, 0, 0, 0, new[] { status.Reason ?? "Push notifications are not configured." });
    }

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];

    // ------------------------------------------------------------------ one pass's shared state

    /// <summary>What every rule in one pass shares, loaded at most once however many rules ask.</summary>
    private sealed class RunContext
    {
        private readonly TimeSpan _grace;
        private readonly Dictionary<string, PushTexts> _texts = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<Guid, SadhanaStreak>? _streaks;
        private Dictionary<(Guid Region, DateOnly Date), PanchangamDay>? _panchangam;
        private IReadOnlyList<Festival>? _festivals;
        private Guid _fallbackRegion;

        public RunContext(DateTime nowUtc, IReadOnlyList<AudienceMember> audience, bool ignoreClock, TimeSpan grace, AccessSnapshot access)
        {
            NowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            Audience = audience;
            IgnoreClock = ignoreClock;
            Access = access;
            _grace = grace;
        }

        public DateTime NowUtc { get; }
        public IReadOnlyList<AudienceMember> Audience { get; }
        public bool IgnoreClock { get; }
        public AccessSnapshot Access { get; }

        /// <summary>India's wall clock: the Panchangam's, and the day sadhana is counted against.</summary>
        public DateTime NowIst => NowUtc + IstOffset;
        public DateOnly TodayIst => DateOnly.FromDateTime(NowIst);

        public DateTime LocalNow(AudienceMember user) => ToLocal(NowUtc, user.TimeZoneId);

        public static DateTime ToLocal(DateTime utc, string? timeZoneId)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    // An id this server does not know. India is where nearly every seeker is.
                }
            }

            return utc + IstOffset;
        }

        /// <summary>
        /// The local date a once-a-day type is due for at this instant, or null when it is not due:
        /// its time has not come, or passed longer ago than the grace period.
        /// </summary>
        public DateOnly? DueDate(AudienceMember user, TimeOnly at)
        {
            var localNow = LocalNow(user);
            var today = DateOnly.FromDateTime(localNow);
            if (IgnoreClock) return today;

            // Yesterday's too: 23:50 with half an hour of grace is still due at 00:10.
            foreach (var date in new[] { today, today.AddDays(-1) })
            {
                var scheduled = date.ToDateTime(at);
                if (scheduled <= localNow && localNow < scheduled + _grace) return date;
            }

            return null;
        }

        /// <summary>The seeker's region, or the fallback for one who never chose. Valid after <see cref="PanchangamAsync"/>.</summary>
        public Guid RegionOf(AudienceMember user) => user.RegionId ?? _fallbackRegion;

        public async Task<PushTexts> TextsAsync(IPushTextProvider provider, string language, CancellationToken cancellationToken)
        {
            if (!_texts.TryGetValue(language, out var texts))
                _texts[language] = texts = await provider.GetAsync(language, cancellationToken);
            return texts;
        }

        public async Task<IReadOnlyDictionary<Guid, SadhanaStreak>> StreaksAsync(INotificationStore store, CancellationToken cancellationToken)
            => _streaks ??= await store.GetStreaksAsync(Audience.Select(a => a.UserId).ToList(), cancellationToken);

        public async Task<Dictionary<(Guid Region, DateOnly Date), PanchangamDay>> PanchangamAsync(INotificationStore store, CancellationToken cancellationToken)
        {
            if (_panchangam is not null) return _panchangam;

            if (Audience.Any(a => a.RegionId is null))
                _fallbackRegion = await store.GetFallbackRegionIdAsync(cancellationToken) ?? Guid.Empty;

            var regions = Audience.Select(RegionOf).Where(id => id != Guid.Empty).Distinct().ToList();

            // A day either side of India's today covers a seeker whose own date differs from it and
            // the window that spills over from yesterday's row.
            var rows = regions.Count == 0
                ? Array.Empty<PanchangamDay>()
                : await store.GetPanchangamAsync(regions, TodayIst.AddDays(-1), TodayIst.AddDays(1), cancellationToken);

            _panchangam = new Dictionary<(Guid, DateOnly), PanchangamDay>();
            foreach (var row in rows.Where(r => r.IsActive))
                _panchangam[(row.RegionId, row.Date)] = row;

            return _panchangam;
        }

        public async Task<IReadOnlyList<Festival>> FestivalsAsync(INotificationStore store, CancellationToken cancellationToken)
            => _festivals ??= (await store.GetFestivalsAsync(TodayIst.AddDays(-1), TodayIst.AddDays(2), cancellationToken))
                .Where(f => f.IsActive).ToList();
    }
}

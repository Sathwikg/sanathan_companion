using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Notifications;

public interface INotificationConsoleService
{
    /// <summary>Everything on the console's first screen except the recurring job's heartbeat, which the API layer adds.</summary>
    Task<ConsoleOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<ConsoleLogPageDto> GetLogsAsync(string? kind, string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsoleRunDto>> GetRunsAsync(string? kind, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsoleDeviceDto>> GetDevicesAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Evaluates one type (or all, when null) now, as if its time had come.</summary>
    Task<ConsoleActionResultDto> RunNowAsync(string? kind, CancellationToken cancellationToken = default);

    Task<ConsoleActionResultDto> SendTestAsync(string kind, Guid userId, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string kind, bool enabled, CancellationToken cancellationToken = default);
    Task<ConsoleActionResultDto> BroadcastAsync(ConsoleBroadcastRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the administrator's notification console shows and does: the engine's types as a job list,
/// what each last did, the delivery history, the registered devices, and the buttons to run, test,
/// pause and broadcast.
/// </summary>
public sealed class NotificationConsoleService : INotificationConsoleService
{
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    private readonly INotificationStore _store;
    private readonly INotificationEngine _engine;
    private readonly INotificationService _notifications;
    private readonly IPushTransport _transport;
    private readonly IPushTextProvider _texts;
    private readonly IUnitOfWork _uow;
    private readonly PushNotificationOptions _options;

    public NotificationConsoleService(
        INotificationStore store,
        INotificationEngine engine,
        INotificationService notifications,
        IPushTransport transport,
        IPushTextProvider texts,
        IUnitOfWork uow,
        IOptions<PushNotificationOptions> options)
    {
        _store = store;
        _engine = engine;
        _notifications = notifications;
        _transport = transport;
        _texts = texts;
        _uow = uow;
        _options = options.Value;
    }

    public async Task<ConsoleOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await _notifications.EnsureBuiltInTypesAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var todayIst = DateOnly.FromDateTime(nowUtc + IstOffset);
        var midnightUtc = DateTime.SpecifyKind(todayIst.ToDateTime(TimeOnly.MinValue) - IstOffset, DateTimeKind.Utc);

        var status = _transport.Status;
        var devices = await _store.GetDevicesAsync(500, cancellationToken);
        var active = devices.Where(d => d.Device.IsActive).ToList();

        var today = (await _store.GetKindCountsAsync(midnightUtc, cancellationToken)).ToDictionary(k => k.Kind, StringComparer.Ordinal);
        var week = (await _store.GetKindCountsAsync(midnightUtc.AddDays(-6), cancellationToken)).ToDictionary(k => k.Kind, StringComparer.Ordinal);
        var days = (await _store.GetDayCountsAsync(midnightUtc.AddDays(-13), cancellationToken)).ToDictionary(d => d.Date);
        var lastRuns = await _store.GetLastRunsAsync(cancellationToken);
        var configs = (await _store.GetConfigsAsync(cancellationToken))
            .GroupBy(c => c.Kind, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var english = await _texts.GetAsync("en", cancellationToken);

        var overview = new ConsoleOverviewDto
        {
            Push = new ConsolePushStatusDto
            {
                Enabled = status.Enabled,
                Ready = status.Ready,
                ProjectId = status.ProjectId,
                Reason = status.Reason,
                DataOnly = _options.DataOnly,
                GraceMinutes = _options.GraceMinutes,
                HistoryDays = _options.HistoryDays
            },
            TotalDevices = devices.Count,
            ActiveDevices = active.Count,
            AndroidDevices = active.Count(d => d.Device.Platform == (int)PushPlatform.Android),
            IosDevices = active.Count(d => d.Device.Platform == (int)PushPlatform.iOS),
            ReachableSeekers = active.Select(d => d.Device.UserId).Distinct().Count(),
            SentToday = today.Values.Sum(k => k.Sent),
            FailedToday = today.Values.Sum(k => k.Failed),
            LastSentUtc = await _store.GetLastSentAtAsync(cancellationToken)
        };

        for (var i = 13; i >= 0; i--)
        {
            var date = todayIst.AddDays(-i);
            days.TryGetValue(date, out var day);
            overview.Days.Add(new ConsoleDayDto { Date = date, Sent = day?.Sent ?? 0, Failed = day?.Failed ?? 0 });
        }

        foreach (var kind in NotificationKinds.All)
        {
            configs.TryGetValue(kind.Code, out var config);
            lastRuns.TryGetValue(kind.Code, out var run);

            overview.Kinds.Add(new ConsoleKindDto
            {
                ConfigId = config?.Id,
                Kind = kind.Code,
                Name = kind.Name,
                Description = kind.Description,
                ModuleName = config?.MenuModule?.Name ?? kind.ModuleCode,
                Icon = config?.MenuModule?.Icon,
                Schedule = kind.Schedule.ToString(),
                SendTime = kind.Schedule is NotificationSchedule.UserTime or NotificationSchedule.DailyAt ? config?.SendTime ?? kind.SendTime : null,
                LeadMinutes = kind.Schedule == NotificationSchedule.BeforeWindow ? config?.LeadMinutes ?? kind.LeadMinutes : null,
                Threshold = kind.Threshold is null ? null : config?.Threshold ?? kind.Threshold,
                TitleTemplate = kind.Schedule == NotificationSchedule.Manual ? null : english.Get(kind.TitleKey),
                BodyTemplate = kind.Schedule == NotificationSchedule.Manual ? null : english.Get(kind.BodyKey),
                IsEnabled = config is { IsActive: true } && (config.MenuModule?.IsActive ?? false),
                DefaultOn = config?.DefaultEnabledForUsers ?? kind.DefaultOn,
                IsMandatory = config?.IsMandatory ?? false,
                SentToday = today.GetValueOrDefault(kind.Code)?.Sent ?? 0,
                FailedToday = today.GetValueOrDefault(kind.Code)?.Failed ?? 0,
                SentLast7Days = week.GetValueOrDefault(kind.Code)?.Sent ?? 0,
                LastRunUtc = run?.StartedAtUtc,
                LastDue = run?.Due,
                LastSent = run?.Sent,
                LastFailed = run?.Failed,
                LastTrigger = run?.Trigger,
                LastError = run?.Error
            });
        }

        return overview;
    }

    public async Task<ConsoleLogPageDto> GetLogsAsync(string? kind, string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (rows, total) = await _store.GetLogsAsync(new NotificationLogQuery(Clean(kind), Clean(status), Clean(search), page, pageSize), cancellationToken);

        return new ConsoleLogPageDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => new ConsoleLogDto
            {
                Id = r.Log.Id,
                UserId = r.Log.UserId,
                UserName = r.UserName,
                Kind = r.Log.Kind,
                Title = r.Log.Title,
                Body = r.Log.Body,
                Status = r.Log.Status,
                Devices = r.Log.Devices,
                Error = r.Log.Error,
                Trigger = r.Log.Trigger,
                CreatedUtc = r.Log.CreatedDate,
                ReadAtUtc = r.Log.ReadAtUtc
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<ConsoleRunDto>> GetRunsAsync(string? kind, int take, CancellationToken cancellationToken = default)
        => (await _store.GetRunsAsync(Clean(kind), take, cancellationToken)).Select(r => new ConsoleRunDto
        {
            Id = r.Id,
            Kind = r.Kind,
            StartedAtUtc = r.StartedAtUtc,
            DurationMs = r.DurationMs,
            Due = r.Due,
            Sent = r.Sent,
            Failed = r.Failed,
            Trigger = r.Trigger,
            Error = r.Error
        }).ToList();

    public async Task<IReadOnlyList<ConsoleDeviceDto>> GetDevicesAsync(int take, CancellationToken cancellationToken = default)
        => (await _store.GetDevicesAsync(take, cancellationToken)).Select(d => new ConsoleDeviceDto
        {
            Id = d.Device.Id,
            UserId = d.Device.UserId,
            UserName = d.UserName,
            Platform = d.Device.Platform,
            DeviceModel = d.Device.DeviceModel,
            AppVersion = d.Device.AppVersion,
            Language = d.Device.Language,
            TimeZone = d.Device.TimeZoneId,
            IsActive = d.Device.IsActive,
            FailureCount = d.Device.FailureCount,
            LastFailureReason = d.Device.LastFailureReason,
            RegisteredUtc = d.Device.CreatedDate,
            LastUsedUtc = d.Device.LastUsedAt,
            TokenTail = d.Device.Token.Length <= 8 ? d.Device.Token : "…" + d.Device.Token[^8..]
        }).ToList();

    public async Task<ConsoleActionResultDto> RunNowAsync(string? kind, CancellationToken cancellationToken = default)
    {
        kind = Clean(kind);
        if (kind is not null && NotificationKinds.Find(kind) is null)
            throw new NotFoundException($"There is no notification type '{kind}'.");

        // One named type is run as if its time had come. "All" is one ordinary pass, on the clock:
        // run by hand it would otherwise send every daily notification of the day at once.
        var summary = await _engine.RunAsync(new EngineRunOptions(
            OnlyKind: kind,
            Trigger: NotificationTriggers.Manual,
            IgnoreClock: kind is not null), cancellationToken);

        if (!summary.Ran)
            return new ConsoleActionResultDto { Ok = false, Message = summary.SkippedReason ?? "Nothing ran." };

        var error = summary.Kinds.Select(k => k.Error).FirstOrDefault(e => e is not null);
        return new ConsoleActionResultDto
        {
            Ok = error is null,
            Due = summary.Due,
            Sent = summary.Sent,
            Failed = summary.Failed,
            Message = error ?? (summary.Due == 0
                ? "Ran. Nobody was due this notification right now."
                : $"Ran. {summary.Sent} sent, {summary.Failed} failed.")
        };
    }

    public async Task<ConsoleActionResultDto> SendTestAsync(string kind, Guid userId, CancellationToken cancellationToken = default)
    {
        if (NotificationKinds.Find(kind) is not { } found)
            throw new NotFoundException($"There is no notification type '{kind}'.");

        var summary = found.Schedule == NotificationSchedule.Manual
            ? await _engine.SendDirectAsync(new DirectNotification(new[] { userId },
                "A Note From " + _options.DefaultTitle,
                "This is how an announcement will look when it arrives.",
                RespectPreferences: false, Trigger: NotificationTriggers.Test), cancellationToken)
            : await _engine.SendTestAsync(userId, kind, cancellationToken);

        return ToResult(summary, sentText: "Sample sent. It should arrive on the phone within a few seconds.");
    }

    public async Task SetEnabledAsync(string kind, bool enabled, CancellationToken cancellationToken = default)
    {
        if (NotificationKinds.Find(kind) is not { } found)
            throw new NotFoundException($"There is no notification type '{kind}'.");

        await _notifications.EnsureBuiltInTypesAsync(cancellationToken);

        var module = (await _uow.MenuModules.GetAllOrderedAsync(cancellationToken)).FirstOrDefault(m => m.Code == found.ModuleCode)
            ?? throw new NotFoundException($"The form this notification belongs to ('{found.ModuleCode}') does not exist.");

        var config = await _uow.NotificationConfigs.GetByModuleAsync(module.Id, found.Code, cancellationToken)
            ?? throw new NotFoundException($"The notification type '{kind}' has no configuration yet.");

        config.IsActive = enabled;
        _uow.NotificationConfigs.Update(config);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConsoleActionResultDto> BroadcastAsync(ConsoleBroadcastRequestDto request, CancellationToken cancellationToken = default)
    {
        var title = Clean(request.Title);
        var body = Clean(request.Body);
        if (title is null || body is null)
            throw new BadRequestException("A broadcast needs both a title and a message.");
        if (title.Length > NotificationLog.MaxTitleLength || body.Length > NotificationLog.MaxBodyLength)
            throw new BadRequestException($"Keep the title within {NotificationLog.MaxTitleLength} characters and the message within {NotificationLog.MaxBodyLength}.");

        var route = Clean(request.Route);
        if (route is not null && !route.StartsWith('/'))
            throw new BadRequestException("The page to open must be an in-app path starting with '/', such as /calendar.");

        var image = Clean(request.ImageUrl);
        if (image is not null && !(Uri.TryCreate(image, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            throw new BadRequestException("The picture must be an https address.");

        var summary = await _engine.SendDirectAsync(new DirectNotification(null, title, body, route, image,
            RespectPreferences: true, Trigger: NotificationTriggers.Broadcast), cancellationToken);

        return ToResult(summary, sentText: $"Broadcast sent to {summary.Sent} seeker(s).");
    }

    private static ConsoleActionResultDto ToResult(DeliverySummary summary, string sentText)
    {
        if (summary.Errors.Count > 0)
            return new ConsoleActionResultDto { Ok = false, Message = summary.Errors[0] };

        if (summary.NoDevice > 0 && summary.Recipients == 0)
            return new ConsoleActionResultDto { Ok = false, Message = "That account has no phone registered. Sign in to the mobile app on a phone, allow notifications, and try again." };

        if (summary.Recipients == 0)
            return new ConsoleActionResultDto { Ok = false, Message = "Nobody could be reached: no seeker with a registered phone is accepting this right now." };

        return new ConsoleActionResultDto
        {
            Ok = summary.Failed == 0,
            Due = summary.Recipients,
            Sent = summary.Sent,
            Failed = summary.Failed,
            Message = summary.Failed == 0 ? sentText : $"{summary.Sent} sent, {summary.Failed} failed. The delivery log has the reason."
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

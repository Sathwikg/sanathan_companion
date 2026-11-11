using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Notifications;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IPushTransport? _transport;

    /// <param name="transport">
    /// Only consulted to tell the phone whether the server can push at all. Optional so the many
    /// callers that never ask (and the tests) need not supply one; absent reads as "cannot".
    /// </param>
    public NotificationService(IUnitOfWork uow, IPushTransport? transport = null)
    {
        _uow = uow;
        _transport = transport;
    }

    private static TimeOnly NowIst => TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    // ---------------- Admin ----------------

    public async Task<NotificationConfigListDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        // A deployment that has just gained a built-in type shows it here straight away.
        await EnsureBuiltInTypesAsync(cancellationToken);

        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var configs = (await _uow.NotificationConfigs.GetAllWithModuleAsync(cancellationToken))
            .ToLookup(c => c.MenuModuleId);

        // Only leaf forms can notify — containers are just navigation.
        var parentIds = modules.Where(m => m.ParentId.HasValue).Select(m => m.ParentId!.Value).ToHashSet();
        var names = modules.ToDictionary(m => m.Id, m => m.Name);

        // Opt-out counts, so the admin can see the impact of each notification type.
        var optOuts = (await _uow.UserNotifications.ListAllAsync(cancellationToken))
            .Where(p => !p.IsEnabled)
            .GroupBy(p => p.NotificationConfigId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new NotificationConfigListDto();
        foreach (var m in modules.Where(m => m.IsActive && !parentIds.Contains(m.Id)))
        {
            var rows = configs[m.Id].ToDictionary(c => c.Kind, StringComparer.Ordinal);

            // The plain reminder is offered on every form, configured or not; the built-in types
            // only on the form they belong to.
            rows.TryGetValue(NotificationKinds.Reminder, out var reminder);
            result.Modules.Add(ToDto(m, names, null, reminder, optOuts));

            foreach (var kind in NotificationKinds.All.Where(k => k.ModuleCode == m.Code))
            {
                rows.TryGetValue(kind.Code, out var cfg);
                result.Modules.Add(ToDto(m, names, kind, cfg, optOuts));
            }
        }

        return result;
    }

    private static NotificationModuleDto ToDto(MenuModule m, Dictionary<Guid, string> names, NotificationKind? kind,
        NotificationConfig? cfg, Dictionary<Guid, int> optOuts) => new()
    {
        MenuModuleId = m.Id,
        ModuleName = m.Name,
        Icon = m.Icon,
        ParentName = m.ParentId.HasValue ? names.GetValueOrDefault(m.ParentId.Value) : null,
        Kind = kind?.Code ?? NotificationKinds.Reminder,
        KindName = kind?.Name ?? "Daily Reminder",
        KindDescription = kind?.Description ?? "A plain reminder at the time each seeker sets, scheduled by their phone.",
        Schedule = (kind?.Schedule ?? NotificationSchedule.Device).ToString(),
        DefaultSendTime = kind?.SendTime,
        DefaultLeadMinutes = kind?.LeadMinutes,
        DefaultThreshold = kind?.Threshold,
        IsEnabled = cfg?.IsActive ?? false,
        Title = cfg?.Title,
        Description = cfg?.Description,
        DefaultEnabledForUsers = cfg?.DefaultEnabledForUsers ?? kind?.DefaultOn ?? true,
        IsMandatory = cfg?.IsMandatory ?? false,
        DisplayOrder = cfg?.DisplayOrder ?? m.DisplayOrder,
        SendTime = cfg?.SendTime,
        LeadMinutes = cfg?.LeadMinutes,
        Threshold = cfg?.Threshold,
        ImageUrl = cfg?.ImageUrl,
        OptedOutUsers = cfg is null ? 0 : optOuts.GetValueOrDefault(cfg.Id)
    };

    public async Task SaveConfigAsync(SaveNotificationConfigDto dto, CancellationToken cancellationToken = default)
    {
        var modules = (await _uow.MenuModules.GetAllOrderedAsync(cancellationToken)).ToDictionary(m => m.Id, m => m);

        foreach (var item in dto.Items)
        {
            if (!modules.TryGetValue(item.MenuModuleId, out var module))
                throw new BadRequestException("One or more selected modules do not exist.");

            var kindCode = string.IsNullOrWhiteSpace(item.Kind) ? NotificationKinds.Reminder : item.Kind.Trim();
            var kind = NotificationKinds.Find(kindCode);

            // A built-in type lives on its own form and nowhere else; anything unknown is refused
            // rather than stored as a type no rule will ever raise.
            if (kindCode != NotificationKinds.Reminder && (kind is null || kind.ModuleCode != module.Code))
                throw new BadRequestException("One or more notification types do not belong to the selected module.");

            if (item.LeadMinutes is < 1 or > 180)
                throw new BadRequestException("The warning lead time must be between 1 and 180 minutes.");
            if (item.Threshold is < 1 or > 365)
                throw new BadRequestException("The streak length must be between 1 and 365 days.");

            var imageUrl = Clean(item.ImageUrl);
            if (imageUrl is not null && !(imageUrl.Length <= 500 && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
                throw new BadRequestException("The picture must be an https address of at most 500 characters.");

            var existing = await _uow.NotificationConfigs.GetByModuleAsync(item.MenuModuleId, kindCode, cancellationToken);

            if (existing is null)
            {
                // Nothing stored and nothing requested — don't create empty rows.
                if (!item.IsEnabled) continue;

                await _uow.NotificationConfigs.AddAsync(new NotificationConfig
                {
                    Id = Guid.NewGuid(),
                    MenuModuleId = item.MenuModuleId,
                    Kind = kindCode,
                    Title = Clean(item.Title),
                    Description = Clean(item.Description),
                    DefaultEnabledForUsers = item.DefaultEnabledForUsers,
                    IsMandatory = item.IsMandatory,
                    DisplayOrder = item.DisplayOrder,
                    SendTime = item.SendTime,
                    LeadMinutes = item.LeadMinutes,
                    Threshold = item.Threshold,
                    ImageUrl = imageUrl,
                    IsActive = true
                }, cancellationToken);
                continue;
            }

            // Turning a type off keeps the row (and every user preference) so re-enabling restores choices.
            existing.IsActive = item.IsEnabled;
            existing.Title = Clean(item.Title);
            existing.Description = Clean(item.Description);
            existing.DefaultEnabledForUsers = item.DefaultEnabledForUsers;
            existing.IsMandatory = item.IsMandatory;
            existing.DisplayOrder = item.DisplayOrder;
            existing.SendTime = item.SendTime;
            existing.LeadMinutes = item.LeadMinutes;
            existing.Threshold = item.Threshold;
            existing.ImageUrl = imageUrl;
            _uow.NotificationConfigs.Update(existing);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> EnsureBuiltInTypesAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await _uow.NotificationConfigs.GetAllWithModuleAsync(cancellationToken))
            .Select(c => (c.MenuModuleId, c.Kind))
            .ToHashSet();

        // Found by the form's code, never by a fixed id: the row belongs to whichever menu row
        // carries that code in this database.
        var byCode = (await _uow.MenuModules.GetAllOrderedAsync(cancellationToken))
            .Where(m => !string.IsNullOrWhiteSpace(m.Code))
            .GroupBy(m => m.Code!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var created = 0;
        for (var i = 0; i < NotificationKinds.All.Count; i++)
        {
            var kind = NotificationKinds.All[i];
            if (!byCode.TryGetValue(kind.ModuleCode, out var module)) continue;
            if (existing.Contains((module.Id, kind.Code))) continue;

            // Title, description and the numbers are left empty on purpose: empty means "follow the
            // built-in", so improving a default later reaches every installation that never changed it.
            await _uow.NotificationConfigs.AddAsync(new NotificationConfig
            {
                Id = Guid.NewGuid(),
                MenuModuleId = module.Id,
                Kind = kind.Code,
                DefaultEnabledForUsers = kind.DefaultOn,
                DisplayOrder = i + 1,
                IsActive = true
            }, cancellationToken);
            created++;
        }

        if (created > 0) await _uow.SaveChangesAsync(cancellationToken);
        return created;
    }

    // ---------------- User ----------------

    public async Task<MyNotificationSettingsDto> GetMySettingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = NowIst;
        var setting = await _uow.UserNotifications.GetSettingAsync(userId, cancellationToken);
        var prefs = (await _uow.UserNotifications.GetPreferencesAsync(userId, cancellationToken))
            .ToDictionary(p => p.NotificationConfigId, p => p);

        var configs = (await _uow.NotificationConfigs.GetAllWithModuleAsync(cancellationToken))
            .Where(c => c.IsActive && (c.MenuModule?.IsActive ?? false))
            .ToList();

        var masterEnabled = setting?.MasterEnabled ?? true;
        var quietEnabled = setting?.QuietHoursEnabled ?? false;
        var inQuiet = quietEnabled && TimeWindow.Contains(setting?.QuietFrom, setting?.QuietTo, now);

        var result = new MyNotificationSettingsDto
        {
            MasterEnabled = masterEnabled,
            QuietHoursEnabled = quietEnabled,
            QuietFrom = setting?.QuietFrom,
            QuietTo = setting?.QuietTo,
            CurrentTime = now,
            InQuietHoursNow = inQuiet,
            PushEnabled = _transport?.Status.Ready ?? false
        };

        foreach (var c in configs.OrderBy(c => c.DisplayOrder).ThenBy(c => c.MenuModule!.Name))
        {
            prefs.TryGetValue(c.Id, out var pref);

            // No saved preference → follow the admin's default. Mandatory types are always on.
            var enabled = c.IsMandatory || (pref?.IsEnabled ?? c.DefaultEnabledForUsers);

            var kind = NotificationKinds.Find(c.Kind);
            var schedule = kind?.Schedule ?? NotificationSchedule.Device;

            var item = new MyNotificationItemDto
            {
                ConfigId = c.Id,
                Title = !string.IsNullOrWhiteSpace(c.Title) ? c.Title! : kind?.Name ?? c.MenuModule?.Name ?? "Notification",
                Description = !string.IsNullOrWhiteSpace(c.Description) ? c.Description : kind?.Description,
                Icon = c.MenuModule?.Icon,
                ModuleName = c.MenuModule?.Name ?? string.Empty,
                Kind = c.Kind,
                Schedule = schedule.ToString(),
                HasCustomText = !string.IsNullOrWhiteSpace(c.Title) || !string.IsNullOrWhiteSpace(c.Description),
                DefaultTime = schedule == NotificationSchedule.UserTime ? c.SendTime ?? kind?.SendTime : null,
                IsMandatory = c.IsMandatory,
                IsEnabled = enabled,
                FromTime = pref?.FromTime,
                ToTime = pref?.ToTime
            };

            // For a type the seeker times themselves the "window" holds one time, not a span to be inside.
            (item.IsActiveNow, item.InactiveReason) = Resolve(item, masterEnabled, inQuiet, now, honourWindow: schedule != NotificationSchedule.UserTime);
            result.Items.Add(item);
        }

        return result;
    }

    /// <summary>
    /// The single place that decides whether a notification may be delivered right now.
    /// NotificationEngine.Reachable applies the same rules when it actually sends.
    /// </summary>
    private static (bool Active, string? Reason) Resolve(MyNotificationItemDto item, bool masterEnabled, bool inQuiet, TimeOnly now, bool honourWindow = true)
    {
        // Mandatory types survive the master switch and an opt-out, but never quiet hours —
        // quiet hours exist so the app stays silent while the seeker rests.
        if (inQuiet) return (false, "Quiet Hours");
        if (!item.IsEnabled) return (false, "Turned Off");
        if (!masterEnabled && !item.IsMandatory) return (false, "All Notifications Paused");

        if (honourWindow && item.FromTime is not null && item.ToTime is not null
            && !TimeWindow.Contains(item.FromTime, item.ToTime, now))
            return (false, "Outside Its Time Window");

        return (true, null);
    }

    public async Task SaveMySettingsAsync(Guid userId, SaveMyNotificationSettingsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.QuietHoursEnabled && (dto.QuietFrom is null || dto.QuietTo is null))
            throw new BadRequestException("Please set both a start and an end time for quiet hours.");

        var schedules = (await _uow.NotificationConfigs.GetAllWithModuleAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => NotificationKinds.ScheduleOf(c.Kind));

        foreach (var i in dto.Items)
        {
            // A type the seeker times themselves stores one time (its start); every other type
            // stores a span, which is only a span with both ends.
            var single = schedules.GetValueOrDefault(i.ConfigId) == NotificationSchedule.UserTime;
            if (!single && i.FromTime is null != i.ToTime is null)
                throw new BadRequestException("A notification window needs both a start and an end time.");
        }

        // ---- global settings ----
        var setting = await _uow.UserNotifications.GetSettingAsync(userId, cancellationToken);
        if (setting is null)
        {
            setting = new UserNotificationSetting { Id = Guid.NewGuid(), UserId = userId };
            await _uow.UserNotifications.AddSettingAsync(setting, cancellationToken);
        }

        setting.MasterEnabled = dto.MasterEnabled;
        setting.QuietHoursEnabled = dto.QuietHoursEnabled;
        setting.QuietFrom = dto.QuietHoursEnabled ? dto.QuietFrom : null;
        setting.QuietTo = dto.QuietHoursEnabled ? dto.QuietTo : null;

        // ---- per-type preferences ----
        var configs = (await _uow.NotificationConfigs.GetAllWithModuleAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c);
        var existing = (await _uow.UserNotifications.GetPreferencesAsync(userId, cancellationToken))
            .ToDictionary(p => p.NotificationConfigId, p => p);

        foreach (var item in dto.Items)
        {
            if (!configs.TryGetValue(item.ConfigId, out var config))
                throw new BadRequestException("One or more notification types do not exist.");

            // Mandatory types cannot be switched off, whatever the client posts.
            var enabled = config.IsMandatory || item.IsEnabled;

            if (existing.TryGetValue(item.ConfigId, out var pref))
            {
                pref.IsEnabled = enabled;
                pref.FromTime = item.FromTime;
                pref.ToTime = item.ToTime;
                _uow.UserNotifications.Update(pref);
            }
            else
            {
                await _uow.UserNotifications.AddAsync(new UserNotificationPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    NotificationConfigId = item.ConfigId,
                    IsEnabled = enabled,
                    FromTime = item.FromTime,
                    ToTime = item.ToTime
                }, cancellationToken);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

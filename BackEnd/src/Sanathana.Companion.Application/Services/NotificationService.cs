using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;

    public NotificationService(IUnitOfWork uow) => _uow = uow;

    private static TimeOnly NowIst => TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    // ---------------- Admin ----------------

    public async Task<NotificationConfigListDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var configs = (await _uow.NotificationConfigs.GetAllWithModuleAsync(cancellationToken))
            .ToDictionary(c => c.MenuModuleId, c => c);

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
            configs.TryGetValue(m.Id, out var cfg);
            result.Modules.Add(new NotificationModuleDto
            {
                MenuModuleId = m.Id,
                ModuleName = m.Name,
                Icon = m.Icon,
                ParentName = m.ParentId.HasValue ? names.GetValueOrDefault(m.ParentId.Value) : null,
                IsEnabled = cfg?.IsActive ?? false,
                Title = cfg?.Title,
                Description = cfg?.Description,
                DefaultEnabledForUsers = cfg?.DefaultEnabledForUsers ?? true,
                IsMandatory = cfg?.IsMandatory ?? false,
                DisplayOrder = cfg?.DisplayOrder ?? m.DisplayOrder,
                OptedOutUsers = cfg is null ? 0 : optOuts.GetValueOrDefault(cfg.Id)
            });
        }

        return result;
    }

    public async Task SaveConfigAsync(SaveNotificationConfigDto dto, CancellationToken cancellationToken = default)
    {
        var modules = (await _uow.MenuModules.GetAllOrderedAsync(cancellationToken)).ToDictionary(m => m.Id, m => m);

        foreach (var item in dto.Items)
        {
            if (!modules.ContainsKey(item.MenuModuleId))
                throw new BadRequestException("One or more selected modules do not exist.");

            var existing = await _uow.NotificationConfigs.GetByModuleAsync(item.MenuModuleId, cancellationToken);

            if (existing is null)
            {
                // Nothing stored and nothing requested — don't create empty rows.
                if (!item.IsEnabled) continue;

                await _uow.NotificationConfigs.AddAsync(new NotificationConfig
                {
                    Id = Guid.NewGuid(),
                    MenuModuleId = item.MenuModuleId,
                    Title = Clean(item.Title),
                    Description = Clean(item.Description),
                    DefaultEnabledForUsers = item.DefaultEnabledForUsers,
                    IsMandatory = item.IsMandatory,
                    DisplayOrder = item.DisplayOrder,
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
            _uow.NotificationConfigs.Update(existing);
        }

        await _uow.SaveChangesAsync(cancellationToken);
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
            InQuietHoursNow = inQuiet
        };

        foreach (var c in configs.OrderBy(c => c.DisplayOrder).ThenBy(c => c.MenuModule!.Name))
        {
            prefs.TryGetValue(c.Id, out var pref);

            // No saved preference → follow the admin's default. Mandatory types are always on.
            var enabled = c.IsMandatory || (pref?.IsEnabled ?? c.DefaultEnabledForUsers);

            var item = new MyNotificationItemDto
            {
                ConfigId = c.Id,
                Title = string.IsNullOrWhiteSpace(c.Title) ? (c.MenuModule?.Name ?? "Notification") : c.Title!,
                Description = c.Description,
                Icon = c.MenuModule?.Icon,
                ModuleName = c.MenuModule?.Name ?? string.Empty,
                IsMandatory = c.IsMandatory,
                IsEnabled = enabled,
                FromTime = pref?.FromTime,
                ToTime = pref?.ToTime
            };

            (item.IsActiveNow, item.InactiveReason) = Resolve(item, masterEnabled, inQuiet, now);
            result.Items.Add(item);
        }

        return result;
    }

    /// <summary>
    /// The single place that decides whether a notification may be delivered right now.
    /// A future delivery job should use exactly this logic.
    /// </summary>
    private static (bool Active, string? Reason) Resolve(MyNotificationItemDto item, bool masterEnabled, bool inQuiet, TimeOnly now)
    {
        // Mandatory types survive the master switch and an opt-out, but never quiet hours —
        // quiet hours exist so the app stays silent while the seeker rests.
        if (inQuiet) return (false, "Quiet hours");
        if (!item.IsEnabled) return (false, "Turned off");
        if (!masterEnabled && !item.IsMandatory) return (false, "All notifications paused");

        if (item.FromTime is not null && item.ToTime is not null
            && !TimeWindow.Contains(item.FromTime, item.ToTime, now))
            return (false, "Outside its time window");

        return (true, null);
    }

    public async Task SaveMySettingsAsync(Guid userId, SaveMyNotificationSettingsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.QuietHoursEnabled && (dto.QuietFrom is null || dto.QuietTo is null))
            throw new BadRequestException("Please set both a start and an end time for quiet hours.");

        foreach (var i in dto.Items)
        {
            if (i.FromTime is null != i.ToTime is null)
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

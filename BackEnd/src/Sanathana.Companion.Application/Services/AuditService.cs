using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Audit;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditConfigCache _cache;
    private readonly IAuditQueue _queue;
    private readonly IValidator<SaveAuditConfigDto> _configValidator;
    private readonly IValidator<LogActivityRequestDto> _activityValidator;
    private readonly IValidator<LogErrorRequestDto> _errorValidator;

    public AuditService(
        IUnitOfWork uow,
        IAuditConfigCache cache,
        IAuditQueue queue,
        IValidator<SaveAuditConfigDto> configValidator,
        IValidator<LogActivityRequestDto> activityValidator,
        IValidator<LogErrorRequestDto> errorValidator)
    {
        _uow = uow;
        _cache = cache;
        _queue = queue;
        _configValidator = configValidator;
        _activityValidator = activityValidator;
        _errorValidator = errorValidator;
    }

    // ------------------------------------------------------------------ configuration

    public async Task<AuditConfigResponseDto> GetConfigAsync(CancellationToken ct = default)
    {
        // A missing row reads as the defaults; nothing is written on a read.
        var settings = await _uow.Audit.GetSettingsAsync(ct) ?? NewDefaultSettings();
        var menuModules = await _uow.MenuModules.GetAllOrderedAsync(ct);
        var configs = (await _uow.Audit.GetModuleConfigsAsync(ct)).ToDictionary(c => c.MenuModuleId);
        var byId = menuModules.ToDictionary(m => m.Id);

        // Only real forms: a container has no route, so there is nothing to visit or edit in it.
        var modules = menuModules
            .Where(m => !string.IsNullOrEmpty(m.Code) && !string.IsNullOrEmpty(m.RoutePath))
            .Select(m =>
            {
                configs.TryGetValue(m.Id, out var existing);
                var parentName = m.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent)
                    ? parent.Name
                    : null;

                return new AuditModuleConfigDto
                {
                    MenuModuleId = m.Id,
                    ModuleName = m.Name,
                    ModuleCode = m.Code ?? string.Empty,
                    Icon = m.Icon,
                    ParentName = parentName,
                    IsActivityAuditEnabled = existing?.IsActivityAuditEnabled ?? true,
                    IsDataAuditEnabled = existing?.IsDataAuditEnabled ?? true
                };
            })
            .ToList();

        return new AuditConfigResponseDto
        {
            Settings = new AuditSettingsDto
            {
                IsGlobalAuditEnabled = settings.IsGlobalAuditEnabled,
                TrackUserSessions = settings.TrackUserSessions,
                TrackPageNavigation = settings.TrackPageNavigation,
                TrackDataModifications = settings.TrackDataModifications,
                TrackErrorLogs = settings.TrackErrorLogs,
                AuditRetentionDays = settings.AuditRetentionDays,
                ErrorRetentionDays = settings.ErrorRetentionDays
            },
            Modules = modules
        };
    }

    public async Task<AuditConfigResponseDto> SaveConfigAsync(SaveAuditConfigDto dto, CancellationToken ct = default)
    {
        await _configValidator.ValidateAndThrowAsync(dto, ct);

        var settings = await _uow.Audit.GetSettingsAsync(ct);
        if (settings is null)
        {
            settings = NewDefaultSettings();
            await _uow.Audit.AddSettingsAsync(settings, ct);
        }

        settings.IsGlobalAuditEnabled = dto.Settings.IsGlobalAuditEnabled;
        settings.TrackUserSessions = dto.Settings.TrackUserSessions;
        settings.TrackPageNavigation = dto.Settings.TrackPageNavigation;
        settings.TrackDataModifications = dto.Settings.TrackDataModifications;
        settings.TrackErrorLogs = dto.Settings.TrackErrorLogs;
        settings.AuditRetentionDays = dto.Settings.AuditRetentionDays;
        settings.ErrorRetentionDays = dto.Settings.ErrorRetentionDays;

        var existing = (await _uow.Audit.GetModuleConfigsAsync(ct)).ToDictionary(c => c.MenuModuleId);
        var forms = (await _uow.MenuModules.GetAllOrderedAsync(ct))
            .Where(m => !string.IsNullOrEmpty(m.Code))
            .ToDictionary(m => m.Id);

        foreach (var mod in dto.Modules)
        {
            if (existing.TryGetValue(mod.MenuModuleId, out var config))
            {
                config.IsActivityAuditEnabled = mod.IsActivityAuditEnabled;
                config.IsDataAuditEnabled = mod.IsDataAuditEnabled;
            }
            else if (forms.TryGetValue(mod.MenuModuleId, out var form))
            {
                // A module id that is not a form is ignored rather than rejected: the screen may
                // have been open while somebody deleted that form.
                await _uow.Audit.AddModuleConfigAsync(new AuditModuleConfig
                {
                    MenuModuleId = mod.MenuModuleId,
                    ModuleCode = form.Code!,
                    IsActivityAuditEnabled = mod.IsActivityAuditEnabled,
                    IsDataAuditEnabled = mod.IsDataAuditEnabled
                }, ct);
            }
        }

        await _uow.SaveChangesAsync(ct);

        // From the database rather than from this request's objects, so the cache also picks up
        // the route map and anything another instance saved in the meantime.
        await _cache.ReloadAsync(ct);

        return await GetConfigAsync(ct);
    }

    // ------------------------------------------------------------------ telemetry from clients

    public async Task RecordActivityAsync(LogActivityRequestDto dto, AuditCaller caller, CancellationToken ct = default)
    {
        await _activityValidator.ValidateAndThrowAsync(dto, ct);

        var routePath = dto.RoutePath.Trim();
        var route = _cache.ResolveRoute(routePath);
        var moduleCode = route?.ModuleCode ?? FirstSegment(routePath);

        if (!_cache.IsActivityAuditEnabled(moduleCode))
            return;

        // The duration is the client's to report, the timestamps are not: a phone whose clock is
        // a day out would otherwise misdate every visit it sends.
        var seconds = Math.Clamp(dto.TimeSpentSeconds, 0, AuditLimits.MaxTimeSpentSeconds);
        var exitedAt = DateTime.UtcNow;

        _queue.EnqueueActivity(new AuditActivityLog
        {
            SessionId = caller.SessionId,
            UserId = caller.UserId,
            UsernameOrEmail = caller.Email,
            ModuleCode = moduleCode,
            FormName = route?.FormName,
            RoutePath = routePath,
            EnteredAtUtc = exitedAt.AddSeconds(-seconds),
            ExitedAtUtc = exitedAt,
            TimeSpentSeconds = seconds,
            Platform = caller.Platform,
            IpAddress = caller.IpAddress
        });
    }

    public async Task RecordErrorAsync(LogErrorRequestDto dto, AuditCaller caller, CancellationToken ct = default)
    {
        await _errorValidator.ValidateAndThrowAsync(dto, ct);

        if (!_cache.TrackErrorLogs)
            return;

        _queue.EnqueueError(new ErrorLog
        {
            TimestampUtc = DateTime.UtcNow,
            Source = dto.Source,
            Severity = dto.Severity,
            StatusCode = dto.StatusCode,
            ExceptionType = dto.ExceptionType ?? string.Empty,
            Message = AuditLimits.Cap(dto.Message, AuditLimits.MaxMessage)!,
            StackTrace = AuditLimits.Cap(dto.StackTrace, AuditLimits.MaxStackTrace),
            InnerException = AuditLimits.Cap(dto.InnerException, AuditLimits.MaxInnerException),
            RequestPath = dto.RequestPath,
            UserId = caller.UserId,
            UsernameOrEmail = caller.Email,
            IpAddress = caller.IpAddress,
            UserAgent = caller.UserAgent
        });
    }

    // ------------------------------------------------------------------ the log screens

    public async Task<PagedResultDto<AuditSessionLogDto>> GetSessionsAsync(AuditLogQueryDto query, CancellationToken ct = default)
    {
        var (filter, page, size) = ToFilter(query);
        var result = await _uow.Audit.QuerySessionsAsync(filter, ct);
        return Page(result, page, size, s => new AuditSessionLogDto
        {
            Id = s.Id,
            UserId = s.UserId,
            UsernameOrEmail = s.UsernameOrEmail,
            LoginTimeUtc = s.LoginTimeUtc,
            LogoutTimeUtc = s.LogoutTimeUtc,
            LastHeartbeatUtc = s.LastHeartbeatUtc,
            DurationSeconds = s.DurationSeconds,
            ExitReason = s.ExitReason,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            Platform = s.Platform
        });
    }

    public async Task<PagedResultDto<AuditActivityLogDto>> GetActivitiesAsync(AuditLogQueryDto query, CancellationToken ct = default)
    {
        var (filter, page, size) = ToFilter(query);
        var result = await _uow.Audit.QueryActivitiesAsync(filter, ct);
        return Page(result, page, size, a => new AuditActivityLogDto
        {
            Id = a.Id,
            SessionId = a.SessionId,
            UserId = a.UserId,
            UsernameOrEmail = a.UsernameOrEmail,
            ModuleCode = a.ModuleCode,
            FormName = a.FormName,
            RoutePath = a.RoutePath,
            EnteredAtUtc = a.EnteredAtUtc,
            ExitedAtUtc = a.ExitedAtUtc,
            TimeSpentSeconds = a.TimeSpentSeconds,
            Platform = a.Platform,
            IpAddress = a.IpAddress
        });
    }

    public async Task<PagedResultDto<AuditDataLogDto>> GetDataLogsAsync(AuditLogQueryDto query, CancellationToken ct = default)
    {
        var (filter, page, size) = ToFilter(query);
        var result = await _uow.Audit.QueryDataLogsAsync(filter, ct);
        return Page(result, page, size, d => new AuditDataLogDto
        {
            Id = d.Id,
            UserId = d.UserId,
            UsernameOrEmail = d.UsernameOrEmail,
            Action = d.Action,
            EntityName = d.EntityName,
            EntityId = d.EntityId,
            ModuleCode = d.ModuleCode,
            ChangedColumns = d.ChangedColumns,
            OldValuesJson = d.OldValuesJson,
            NewValuesJson = d.NewValuesJson,
            TimestampUtc = d.TimestampUtc,
            IpAddress = d.IpAddress,
            Endpoint = d.Endpoint
        });
    }

    public async Task<PagedResultDto<ErrorLogDto>> GetErrorsAsync(AuditLogQueryDto query, CancellationToken ct = default)
    {
        var (filter, page, size) = ToFilter(query);
        var result = await _uow.Audit.QueryErrorsAsync(filter, ct);
        return Page(result, page, size, ToDto);
    }

    public async Task<ErrorLogDto?> ResolveErrorAsync(Guid errorId, string resolvedBy, CancellationToken ct = default)
    {
        var err = await _uow.Audit.GetErrorByIdAsync(errorId, ct);
        if (err is null) return null;

        if (!err.IsResolved)
        {
            err.IsResolved = true;
            err.ResolvedAtUtc = DateTime.UtcNow;
            err.ResolvedBy = AuditLimits.Cap(resolvedBy, 100);
            await _uow.SaveChangesAsync(ct);
        }

        return ToDto(err);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The seed creates the row; this only covers a database where it was deleted by hand.</summary>
    private static AuditSettings NewDefaultSettings() => new();

    private static string FirstSegment(string routePath)
    {
        var segment = routePath.Trim('/').Split('/', '?', '#')[0];
        return string.IsNullOrEmpty(segment) ? "home" : segment.ToLowerInvariant();
    }

    private static (AuditLogFilter Filter, int Page, int PageSize) ToFilter(AuditLogQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize <= 0 ? AuditLimits.DefaultPageSize : query.PageSize, 1, AuditLimits.MaxPageSize);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : AuditLimits.Cap(query.Search.Trim(), 100);

        var filter = new AuditLogFilter(
            Skip: (page - 1) * size,
            Take: size,
            Search: search,
            FromUtc: query.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            // Inclusive of the whole "to" day.
            ToUtc: query.To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Action: string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim().ToUpperInvariant(),
            Source: string.IsNullOrWhiteSpace(query.Source) ? null : query.Source.Trim(),
            Resolved: query.Resolved);

        return (filter, page, size);
    }

    private static PagedResultDto<TDto> Page<TEntity, TDto>(AuditPage<TEntity> result, int page, int size, Func<TEntity, TDto> map)
        => new()
        {
            Items = result.Items.Select(map).ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = size
        };

    private static ErrorLogDto ToDto(ErrorLog e) => new()
    {
        Id = e.Id,
        TimestampUtc = e.TimestampUtc,
        Source = e.Source,
        Severity = e.Severity,
        StatusCode = e.StatusCode,
        ExceptionType = e.ExceptionType,
        Message = e.Message,
        StackTrace = e.StackTrace,
        InnerException = e.InnerException,
        RequestPath = e.RequestPath,
        RequestMethod = e.RequestMethod,
        UsernameOrEmail = e.UsernameOrEmail,
        IpAddress = e.IpAddress,
        UserAgent = e.UserAgent,
        IsResolved = e.IsResolved,
        ResolvedAtUtc = e.ResolvedAtUtc,
        ResolvedBy = e.ResolvedBy
    };
}

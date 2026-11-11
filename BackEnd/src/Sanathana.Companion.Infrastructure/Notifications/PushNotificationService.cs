using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Notifications;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Notifications;

/// <summary>
/// The administrator's hand-sent notifications and the device list behind them.
/// </summary>
/// <remarks>
/// Everything that is sent goes through <see cref="INotificationEngine"/>, the same path the
/// scheduled notifications take, so a hand-sent message is written to the delivery history, lands
/// in each seeker's inbox and retires dead device tokens exactly as a scheduled one does.
/// </remarks>
public sealed class PushNotificationService : IPushNotificationService
{
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    private readonly IDeviceTokenRepository _deviceTokenRepo;
    private readonly IUnitOfWork _uow;
    private readonly INotificationStore _store;
    private readonly INotificationEngine _engine;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        IDeviceTokenRepository deviceTokenRepo,
        IUnitOfWork uow,
        INotificationStore store,
        INotificationEngine engine,
        ILogger<PushNotificationService> logger)
    {
        _deviceTokenRepo = deviceTokenRepo;
        _uow = uow;
        _store = store;
        _engine = engine;
        _logger = logger;
    }

    // ========== Device Token Management ==========

    public async Task<DeviceTokenDto> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
        => MapToDto(await _store.UpsertDeviceAsync(userId, (int)request.Platform, request.Token, request.DeviceModel, request.AppVersion, null, null, cancellationToken));

    public async Task<IReadOnlyList<DeviceTokenDto>> GetUserDeviceTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await _deviceTokenRepo.GetActiveByUserAsync(userId, cancellationToken)).Select(MapToDto).ToList();

    public async Task<bool> RemoveDeviceTokenAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var existing = await _deviceTokenRepo.GetByTokenAsync(token, cancellationToken);
        if (existing == null || existing.UserId != userId) return false;

        return await _store.RemoveDeviceAsync(token, cancellationToken);
    }

    public async Task RemoveAllUserDeviceTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var token in await _deviceTokenRepo.GetActiveByUserAsync(userId, cancellationToken))
            await _store.RemoveDeviceAsync(token.Token, cancellationToken);
    }

    public async Task MarkTokenInactiveAsync(string token, string reason, CancellationToken cancellationToken = default)
    {
        await _deviceTokenRepo.MarkInactiveAsync(token, reason, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    // ========== Sending Notifications ==========

    public async Task<PushNotificationResult> SendToUsersAsync(SendPushNotificationRequest request, CancellationToken cancellationToken = default)
    {
        // The administrator named these seekers, so their Announcements switch is not consulted;
        // quiet hours are not either, because the administrator can see the clock.
        var summary = await _engine.SendDirectAsync(new DirectNotification(
            request.UserIds.Distinct().ToList(),
            request.Title,
            request.Body,
            Route: request.Data is not null && request.Data.TryGetValue("route", out var route) ? route : null,
            ImageUrl: request.ImageUrl,
            RespectPreferences: false,
            Trigger: NotificationTriggers.Manual), cancellationToken);

        return ToResult(summary);
    }

    public async Task<PushNotificationResult> SendToModuleSubscribersAsync(SendModuleNotificationRequest request, CancellationToken cancellationToken = default)
    {
        // Seekers who have this form's notifications switched on.
        var tokens = await _deviceTokenRepo.GetActiveByModuleAsync(request.MenuModuleId,
            request.TargetPlatform.HasValue ? (int?)request.TargetPlatform.Value : null,
            cancellationToken);

        var userIds = tokens.Select(t => t.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            _logger.LogInformation("No active subscribers for module {ModuleId}", request.MenuModuleId);
            return new PushNotificationResult(true, 0, 0, new List<string>());
        }

        var summary = await _engine.SendDirectAsync(new DirectNotification(
            userIds,
            request.Title,
            request.Body,
            Route: request.Data is not null && request.Data.TryGetValue("route", out var route) ? route : null,
            ImageUrl: request.ImageUrl,
            RespectPreferences: false,
            Trigger: NotificationTriggers.Broadcast), cancellationToken);

        return ToResult(summary);
    }

    public async Task<PushNotificationResult> SendTestNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        var summary = await _engine.SendDirectAsync(new DirectNotification(
            new[] { userId }, title, body, RespectPreferences: false, Trigger: NotificationTriggers.Test), cancellationToken);

        return ToResult(summary);
    }

    private static PushNotificationResult ToResult(DeliverySummary summary)
    {
        var errors = summary.Errors.ToList();
        if (summary.NoDevice > 0)
            errors.Add(summary.NoDevice == 1
                ? "1 seeker has no device registered, so nothing could be sent to them."
                : $"{summary.NoDevice} seekers have no device registered, so nothing could be sent to them.");

        // "Success" means nothing that was attempted was refused; a seeker with no phone is reported
        // in Errors but is not a delivery failure.
        return new PushNotificationResult(summary.Failed == 0 && summary.Errors.Count == 0, summary.Sent, summary.Failed, errors);
    }

    // ========== Statistics ==========

    public async Task<PushNotificationStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var allTokens = await _deviceTokenRepo.GetAllAsync(cancellationToken);
        var activeTokens = allTokens.Where(t => t.IsActive).ToList();

        // "Today" is India's day, as everywhere else on the server.
        var istMidnightUtc = (DateTime.UtcNow + IstOffset).Date - IstOffset;
        var today = await _store.GetKindCountsAsync(DateTime.SpecifyKind(istMidnightUtc, DateTimeKind.Utc), cancellationToken);

        return new PushNotificationStatsDto(
            TotalDevices: allTokens.Count,
            ActiveDevices: activeTokens.Count,
            AndroidDevices: activeTokens.Count(t => t.Platform == (int)PushPlatform.Android),
            iOSDevices: activeTokens.Count(t => t.Platform == (int)PushPlatform.iOS),
            NotificationsSentToday: today.Sum(k => k.Sent),
            NotificationsFailedToday: today.Sum(k => k.Failed),
            LastSentAt: await _store.GetLastSentAtAsync(cancellationToken) ?? default);
    }

    public async Task<int> CleanupInactiveTokensAsync(int olderThanDays = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var deleted = await _deviceTokenRepo.DeleteInactiveOlderThanAsync(cutoff, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} inactive device tokens older than {Days} days", deleted, olderThanDays);
        return deleted;
    }

    private static DeviceTokenDto MapToDto(DeviceToken token) => new(
        Id: token.Id,
        UserId: token.UserId,
        Platform: (PushPlatform)token.Platform,
        Token: token.Token,
        DeviceModel: token.DeviceModel,
        AppVersion: token.AppVersion,
        CreatedAt: token.CreatedDate,
        LastUsedAt: token.LastUsedAt,
        IsActive: token.IsActive);
}

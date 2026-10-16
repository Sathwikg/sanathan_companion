using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Notifications;

/// <summary>
/// Push notification service using FCM (Android) and APNs (iOS).
/// </summary>
public sealed class PushNotificationService : IPushNotificationService
{
    private readonly IDeviceTokenRepository _deviceTokenRepo;
    private readonly IUnitOfWork _uow;
    private readonly IOptions<PushNotificationOptions> _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly FirebaseMessaging? _fcmMessaging;
    private readonly bool _fcmInitialized;
    private readonly bool _apnsInitialized;

    public PushNotificationService(
        IDeviceTokenRepository deviceTokenRepo,
        IUnitOfWork uow,
        IOptions<PushNotificationOptions> options,
        ILogger<PushNotificationService> logger)
    {
        _deviceTokenRepo = deviceTokenRepo;
        _uow = uow;
        _options = options;
        _logger = logger;

        // Initialize FCM
        _fcmInitialized = InitializeFcm(out _fcmMessaging);
        
        // Initialize APNs (placeholder - requires certificate setup)
        _apnsInitialized = InitializeApns();
    }

    private bool InitializeFcm(out FirebaseMessaging? messaging)
    {
        messaging = null;
        var opts = _options.Value;
        if (!opts.Enabled || !opts.IsFcmConfigured)
        {
            _logger.LogInformation("FCM not configured, push notifications to Android will be disabled");
            return false;
        }

        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                GoogleCredential credential;
                
                if (!string.IsNullOrWhiteSpace(opts.FcmServiceAccountPath))
                {
                    // Load from file path
#pragma warning disable CS0618 // Obsolete API but still works for file-based credentials
                    credential = GoogleCredential.FromFile(opts.FcmServiceAccountPath);
#pragma warning restore CS0618
                }
                else if (!string.IsNullOrWhiteSpace(opts.FcmProjectId))
                {
                    // Use Application Default Credentials
                    credential = GoogleCredential.GetApplicationDefault();
                }
                else
                {
                    _logger.LogWarning("FCM enabled but no credentials configured");
                    return false;
                }

                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = opts.FcmProjectId
                });
            }

            messaging = FirebaseMessaging.DefaultInstance;
            _logger.LogInformation("FCM initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FCM");
            return false;
        }
    }

    private bool InitializeApns()
    {
        var opts = _options.Value;
        if (!opts.Enabled || !opts.IsApnsConfigured)
        {
            _logger.LogInformation("APNs not configured, push notifications to iOS will be disabled");
            return false;
        }

        // APNs initialization would go here
        // Requires: ApnsKeyId, ApnsTeamId, ApnsPrivateKeyPath, ApnsBundleId
        // Using HTTP/2 with JWT authentication
        _logger.LogWarning("APNs initialization not fully implemented - requires HTTP/2 client setup");
        return false;
    }

    // ========== Device Token Management ==========

    public async Task<DeviceTokenDto> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        var platform = (int)request.Platform;
        
        // Check if token already exists
        var existing = await _deviceTokenRepo.GetByTokenAsync(request.Token, cancellationToken);
        if (existing != null)
        {
            // Update existing
            existing.UserId = userId;
            existing.Platform = platform;
            existing.DeviceModel = request.DeviceModel;
            existing.AppVersion = request.AppVersion;
            existing.IsActive = true;
            existing.FailureCount = 0;
            existing.LastFailureReason = null;
            _deviceTokenRepo.Update(existing);
            await _uow.SaveChangesAsync(cancellationToken);
            
            return MapToDto(existing);
        }

        // Create new
        var token = new DeviceToken
        {
            UserId = userId,
            Platform = platform,
            Token = request.Token,
            DeviceModel = request.DeviceModel,
            AppVersion = request.AppVersion,
            IsActive = true
        };

        await _deviceTokenRepo.AddAsync(token, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return MapToDto(token);
    }

    public async Task<IReadOnlyList<DeviceTokenDto>> GetUserDeviceTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _deviceTokenRepo.GetActiveByUserAsync(userId, cancellationToken);
        return tokens.Select(MapToDto).ToList();
    }

    public async Task<bool> RemoveDeviceTokenAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var existing = await _deviceTokenRepo.GetByTokenAsync(token, cancellationToken);
        if (existing == null || existing.UserId != userId)
            return false;

        _deviceTokenRepo.Remove(existing);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RemoveAllUserDeviceTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _deviceTokenRepo.GetActiveByUserAsync(userId, cancellationToken);
        foreach (var token in tokens)
        {
            _deviceTokenRepo.Remove(token);
        }
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkTokenInactiveAsync(string token, string reason, CancellationToken cancellationToken = default)
    {
        await _deviceTokenRepo.MarkInactiveAsync(token, reason, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    // ========== Sending Notifications ==========

    public async Task<PushNotificationResult> SendToUsersAsync(SendPushNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var sentCount = 0;
        var failedCount = 0;
        var failedTokens = new Dictionary<Guid, string>();

        foreach (var userId in request.UserIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var tokens = await _deviceTokenRepo.GetActiveByUserAsync(userId, cancellationToken);
                
                // Filter by platform if specified
                if (request.TargetPlatform.HasValue)
                {
                    tokens = tokens.Where(t => t.Platform == (int)request.TargetPlatform.Value).ToList();
                }

                if (!tokens.Any())
                {
                    _logger.LogDebug("No active device tokens for user {UserId} (platform: {Platform})", 
                        userId, request.TargetPlatform?.ToString() ?? "any");
                    continue;
                }

                // Send to each token
                foreach (var token in tokens)
                {
                    var result = await SendToTokenAsync(token, request, cancellationToken);
                    if (result)
                    {
                        sentCount++;
                        await _deviceTokenRepo.UpdateLastUsedAsync(token.Token, DateTime.UtcNow, cancellationToken);
                        await _deviceTokenRepo.ResetFailureCountAsync(token.Token, cancellationToken);
                    }
                    else
                    {
                        failedCount++;
                        failedTokens[userId] = token.Token;
                        await _deviceTokenRepo.IncrementFailureCountAsync(token.Token, "Send failed", cancellationToken);
                        
                        // If too many failures, mark inactive
                        if (token.FailureCount >= _options.Value.MaxRetryAttempts)
                        {
                            await MarkTokenInactiveAsync(token.Token, $"Max retries ({_options.Value.MaxRetryAttempts}) exceeded", cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
                errors.Add($"User {userId}: {ex.Message}");
                failedCount++;
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return new PushNotificationResult(
            Success: failedCount == 0,
            SentCount: sentCount,
            FailedCount: failedCount,
            Errors: errors,
            FailedTokens: failedTokens.Count > 0 ? failedTokens : null
        );
    }

    public async Task<PushNotificationResult> SendToModuleSubscribersAsync(SendModuleNotificationRequest request, CancellationToken cancellationToken = default)
    {
        // Get tokens for users who have this module's notifications enabled
        var tokens = await _deviceTokenRepo.GetActiveByModuleAsync(request.MenuModuleId, 
            request.TargetPlatform.HasValue ? (int?)request.TargetPlatform.Value : null, 
            cancellationToken);

        if (!tokens.Any())
        {
            _logger.LogInformation("No active subscribers for module {ModuleId}", request.MenuModuleId);
            return new PushNotificationResult(true, 0, 0, new List<string>());
        }

        var userIds = tokens.Select(t => t.UserId).Distinct().ToList();
        
        var sendRequest = new SendPushNotificationRequest(
            UserIds: userIds,
            Title: request.Title,
            Body: request.Body,
            Data: request.Data,
            ImageUrl: request.ImageUrl,
            TargetPlatform: request.TargetPlatform
        );

        return await SendToUsersAsync(sendRequest, cancellationToken);
    }

    public async Task<PushNotificationResult> SendTestNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        var request = new SendPushNotificationRequest(
            UserIds: [userId],
            Title: title,
            Body: body,
            Data: new Dictionary<string, string> { { "test", "true" } }
        );

        return await SendToUsersAsync(request, cancellationToken);
    }

    private async Task<bool> SendToTokenAsync(DeviceToken token, SendPushNotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (token.Platform == (int)PushPlatform.Android)
            {
                return await SendFcmAsync(token.Token, request, cancellationToken);
            }
            else if (token.Platform == (int)PushPlatform.iOS)
            {
                return await SendApnsAsync(token.Token, request, cancellationToken);
            }

            _logger.LogWarning("Unknown platform {Platform} for token {Token}", token.Platform, token.Token[..10]);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send to token {Token}", token.Token[..10]);
            return false;
        }
    }

    private async Task<bool> SendFcmAsync(string fcmToken, SendPushNotificationRequest request, CancellationToken cancellationToken)
    {
        if (!_fcmInitialized || _fcmMessaging == null)
        {
            _logger.LogWarning("FCM not initialized, skipping Android notification");
            return false;
        }

        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = request.Title,
                    Body = request.Body,
                    ImageUrl = request.ImageUrl
                },
                Data = request.Data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Icon = _options.Value.DefaultIcon,
                        Color = _options.Value.DefaultColor,
                        Sound = request.Sound ?? "default",
                        ChannelId = "default"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Alert = new ApsAlert
                        {
                            Title = request.Title,
                            Body = request.Body
                        },
                        Badge = request.Badge,
                        Sound = request.Sound ?? "default"
                    }
                }
            };

            var response = await _fcmMessaging.SendAsync(message, cancellationToken);
            _logger.LogDebug("FCM message sent: {Response}", response);
            return true;
        }
        catch (FirebaseMessagingException ex) when (IsUnrecoverableFcmError(ex.ErrorCode))
        {
            // Token is invalid, mark as inactive
            await MarkTokenInactiveAsync(fcmToken, $"FCM error: {ex.ErrorCode}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed for token {Token}", fcmToken[..10]);
            return false;
        }
    }

    /// <summary>
    /// Determines if an FCM error code represents an unrecoverable error (token invalid, etc.)
    /// </summary>
    private static bool IsUnrecoverableFcmError(ErrorCode errorCode)
    {
        // ErrorCode is from Google.Apis - check string representation for known unrecoverable errors
        var code = errorCode.ToString();
        return code switch
        {
            "UNREGISTERED" => true,           // Token invalid/unregistered
            "INVALID_ARGUMENT" => true,       // Bad token format
            "SENDER_ID_MISMATCH" => true,     // Sender ID mismatch
            "QUOTA_EXCEEDED" => true,         // Project quota exceeded
            _ => false
        };
    }

    private async Task<bool> SendApnsAsync(string apnsToken, SendPushNotificationRequest request, CancellationToken cancellationToken)
    {
        if (!_apnsInitialized)
        {
            _logger.LogWarning("APNs not initialized, skipping iOS notification");
            return false;
        }

        // APNs HTTP/2 implementation would go here
        // For now, return false to indicate not implemented
        _logger.LogWarning("APNs send not implemented yet");
        return false;
    }

    // ========== Statistics ==========

    public async Task<PushNotificationStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var allTokens = await _deviceTokenRepo.GetAllAsync(cancellationToken);
        var activeTokens = allTokens.Where(t => t.IsActive).ToList();
        var today = DateTime.UtcNow.Date;

        // TODO: Add actual sent/failed tracking with a notification log table
        return new PushNotificationStatsDto(
            TotalDevices: allTokens.Count,
            ActiveDevices: activeTokens.Count,
            AndroidDevices: activeTokens.Count(t => t.Platform == (int)PushPlatform.Android),
            iOSDevices: activeTokens.Count(t => t.Platform == (int)PushPlatform.iOS),
            NotificationsSentToday: 0, // Would need a notification log table
            NotificationsFailedToday: 0,
            LastSentAt: DateTime.UtcNow
        );
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
        IsActive: token.IsActive
    );
}
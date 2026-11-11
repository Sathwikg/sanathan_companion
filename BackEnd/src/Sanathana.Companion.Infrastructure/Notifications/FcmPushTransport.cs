using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Notifications;

/// <summary>
/// Firebase Cloud Messaging. Android phones are reached directly; an iPhone would be reached
/// through Firebase's own relay to Apple once the iOS app registers a Firebase token.
/// </summary>
/// <remarks>
/// A singleton: the Firebase app holds an OAuth token and an HTTP client that are meant to be
/// created once per process. It is built on first use rather than at start-up, so a bad key file
/// costs push and nothing else, and the reason is kept for the console to show.
/// </remarks>
public sealed class FcmPushTransport : IPushTransport
{
    private const string AppName = "nitya-push";

    /// <summary>The most messages Firebase accepts in one SendEach call.</summary>
    private const int BatchLimit = 500;

    private readonly PushNotificationOptions _options;
    private readonly ILogger<FcmPushTransport> _logger;
    private readonly Lazy<(FirebaseMessaging? Messaging, PushTransportStatus Status)> _state;

    public FcmPushTransport(IOptions<PushNotificationOptions> options, ILogger<FcmPushTransport> logger)
    {
        _options = options.Value;
        _logger = logger;
        _state = new Lazy<(FirebaseMessaging?, PushTransportStatus)>(Initialise, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public PushTransportStatus Status => _state.Value.Status;

    private (FirebaseMessaging?, PushTransportStatus) Initialise()
    {
        if (!_options.Enabled)
            return (null, new PushTransportStatus(false, false, _options.FcmProjectId, "Push notifications are switched off (PushNotifications:Enabled is false)."));

        try
        {
            var (json, source) = ReadServiceAccount();
            if (json is null)
                return (null, new PushTransportStatus(true, false, _options.FcmProjectId,
                    "No Firebase service account key was found. Set PushNotifications:FcmServiceAccountPath to the key file, or PushNotifications:FcmServiceAccountJson to its content."));

            var projectId = string.IsNullOrWhiteSpace(_options.FcmProjectId) ? ProjectIdOf(json) : _options.FcmProjectId;

#pragma warning disable CS0618 // FromJson is flagged for untrusted input; this is our own key file.
            var credential = GoogleCredential.FromJson(json);
#pragma warning restore CS0618

            // Named, so this never collides with a default app another library might create.
            var app = FirebaseApp.GetInstance(AppName) ?? FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = projectId }, AppName);

            _logger.LogInformation("Firebase Cloud Messaging ready for project {ProjectId} (key from {Source})", projectId, source);
            return (FirebaseMessaging.GetMessaging(app), new PushTransportStatus(true, true, projectId, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firebase Cloud Messaging could not be initialised");
            return (null, new PushTransportStatus(true, false, _options.FcmProjectId, "The Firebase service account key could not be used: " + ex.Message));
        }
    }

    /// <summary>The key as JSON text, from the inline setting (raw or base64) or else from the file.</summary>
    private (string? Json, string Source) ReadServiceAccount()
    {
        var inline = _options.FcmServiceAccountJson?.Trim();
        if (!string.IsNullOrEmpty(inline))
        {
            if (inline.StartsWith('{')) return (inline, "FcmServiceAccountJson");
            return (Encoding.UTF8.GetString(Convert.FromBase64String(inline)), "FcmServiceAccountJson (base64)");
        }

        var path = _options.FcmServiceAccountPath?.Trim();
        if (string.IsNullOrEmpty(path)) return (null, "nothing");

        // Older settings put the JSON itself in the "path" setting; honour that rather than fail on it.
        if (path.StartsWith('{')) return (path, "FcmServiceAccountPath (inline)");

        if (!File.Exists(path))
            throw new FileNotFoundException($"The file named in PushNotifications:FcmServiceAccountPath does not exist: {path}");

        return (File.ReadAllText(path), "FcmServiceAccountPath");
    }

    private static string? ProjectIdOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("project_id", out var id) ? id.GetString() : null;
    }

    public async Task<IReadOnlyList<PushSendOutcome>> SendAsync(IReadOnlyList<PushEnvelope> envelopes, CancellationToken cancellationToken = default)
    {
        var (messaging, status) = _state.Value;
        if (messaging is null)
            return envelopes.Select(e => new PushSendOutcome(e.DeviceId, false, false, status.Reason)).ToList();

        var outcomes = new List<PushSendOutcome>(envelopes.Count);

        foreach (var batch in envelopes.Chunk(BatchLimit))
        {
            var response = await messaging.SendEachAsync(batch.Select(Build).ToList(), cancellationToken);

            for (var i = 0; i < batch.Length; i++)
            {
                var result = response.Responses[i];
                if (result.IsSuccess)
                {
                    outcomes.Add(new PushSendOutcome(batch[i].DeviceId, true, false, null));
                    continue;
                }

                var code = result.Exception?.MessagingErrorCode;

                // Only these two mean "this token will never work": the app was uninstalled or its
                // data cleared, or the token belongs to another Firebase project. InvalidArgument is
                // deliberately not here: it is also what a malformed message returns, and treating
                // it as a dead token would retire every phone over one bad payload.
                var dead = code is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch;

                outcomes.Add(new PushSendOutcome(batch[i].DeviceId, false, dead,
                    $"{code?.ToString() ?? result.Exception?.ErrorCode.ToString() ?? "Error"}: {result.Exception?.Message}"));
            }
        }

        return outcomes;
    }

    private Message Build(PushEnvelope envelope)
    {
        var m = envelope.Message;
        var ttl = m.Ttl ?? TimeSpan.FromSeconds(Math.Max(60, _options.TimeToLiveSeconds));

        // Everything the app needs to draw the notification itself and to open the right page.
        // Present on every message, with or without a notification part.
        var data = new Dictionary<string, string>
        {
            ["kind"] = m.Kind,
            ["channel"] = m.Channel,
            ["title"] = m.Title,
            ["body"] = m.Body
        };
        if (!string.IsNullOrWhiteSpace(m.Route)) data["route"] = m.Route!;
        if (!string.IsNullOrWhiteSpace(m.ImageUrl)) data["image"] = m.ImageUrl!;
        if (!string.IsNullOrWhiteSpace(m.Tag)) data["tag"] = m.Tag!;

        var message = new Message { Token = envelope.Token, Data = data };

        if (envelope.Platform == (int)PushPlatform.iOS)
        {
            message.Notification = new Notification { Title = m.Title, Body = m.Body, ImageUrl = m.ImageUrl };
            message.Apns = new ApnsConfig
            {
                Aps = new Aps
                {
                    Alert = new ApsAlert { Title = m.Title, Body = m.Body },
                    Sound = "default",
                    ThreadId = m.Channel
                }
            };
            return message;
        }

        message.Android = new AndroidConfig
        {
            // High, so a phone in Doze is woken for it: a reminder that waits for the next
            // maintenance window is a reminder that arrives late.
            Priority = Priority.High,
            TimeToLive = ttl
        };

        if (!_options.DataOnly)
        {
            // The notification part is what Google Play services draws by itself when the app is
            // not running. Icon, colour and channel are named here so that drawing matches what the
            // app's own renderer produces when it is.
            message.Notification = new Notification { Title = m.Title, Body = m.Body, ImageUrl = m.ImageUrl };
            message.Android.Notification = new AndroidNotification
            {
                ChannelId = m.Channel,
                Icon = string.IsNullOrWhiteSpace(_options.DefaultIcon) ? null : _options.DefaultIcon,
                Color = string.IsNullOrWhiteSpace(_options.DefaultColor) ? null : _options.DefaultColor,
                Tag = m.Tag
            };
        }

        return message;
    }
}

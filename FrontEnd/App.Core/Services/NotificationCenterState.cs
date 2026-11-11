using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// Shared state for the mobile notification centre: the seeker's inbox and settings behind the
/// bell, whether the panel is open, and this phone's registration for push.
/// </summary>
/// <remarks>
/// The bell and the panel are two components in two different places in the DOM — the bell lives in
/// the top bar, and the panel must NOT, because the bar's <c>backdrop-filter</c> makes it the
/// containing block for any <c>position: fixed</c> descendant. A fixed scrim rendered inside the
/// bar collapses to the bar's own box, so tapping outside it would not dismiss anything. This
/// service is what lets the two be siblings while sharing one fetch and one open/closed flag.
/// <para>
/// Scoped, like <see cref="ToastService"/> and <see cref="ConfirmService"/>, so it resets with the
/// user session.
/// </para>
/// </remarks>
public class NotificationCenterState : IUserSessionState
{
    private readonly IApiClient _api;
    private readonly IReminderScheduler _reminders;
    private readonly IPushRegistrar _push;
    private readonly LocalizationState _loc;

    /// <summary>The token this phone last registered with the server; null until it has.</summary>
    private string? _registeredToken;

    /// <summary>The language that registration carried, so a language change re-registers.</summary>
    private string? _registeredLanguage;

    public NotificationCenterState(IApiClient api, IReminderScheduler reminders, IPushRegistrar push, LocalizationState loc)
    {
        _api = api;
        _reminders = reminders;
        _push = push;
        _loc = loc;
    }

    public MyNotificationSettings? Settings { get; private set; }

    /// <summary>The notifications this seeker was sent, newest first. Empty until fetched.</summary>
    public IReadOnlyList<InboxItem> Inbox { get; private set; } = Array.Empty<InboxItem>();

    /// <summary>How many of them the seeker has not seen: the bell's badge.</summary>
    public int Unread { get; private set; }

    public bool IsOpen { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    /// <summary>Raised whenever the badge, the panel's contents or its open state change.</summary>
    public event Action? OnChanged;

    /// <summary>How many reminder types the server says would fire at this moment.</summary>
    public int ActiveCount => Settings?.Items.Count(i => i.IsActiveNow) ?? 0;

    /// <summary>Quiet hours or the master switch — either way nothing will reach the seeker.</summary>
    public bool IsMuted => Settings is not null && (!Settings.MasterEnabled || Settings.InQuietHoursNow);

    /// <summary>
    /// True when the server can push and this phone is on its list. The server then sends the
    /// sadhana reminder itself, and the phone must not also schedule it.
    /// </summary>
    public bool PushDelivers => _registeredToken is not null && (Settings?.PushEnabled ?? false);

    /// <summary>
    /// Fills the badge without disturbing the UI. Best-effort on purpose: a phone with no signal
    /// should still get its shell, just without a count.
    /// </summary>
    public async Task PrimeAsync()
    {
        if (Settings is not null) return;

        try
        {
            Settings = await _api.GetMyNotificationsAsync();
            OnChanged?.Invoke();
        }
        catch
        {
            // No badge is a better answer than a wrong one.
            return;
        }

        // Registration first: whether push reaches this phone decides what the phone schedules.
        await RegisterDeviceAsync();
        await SyncRemindersAsync();
        await RefreshInboxAsync();
    }

    /// <summary>
    /// Tells the server about this phone, so notifications can reach it in its language and on its
    /// clock. Safe to call often: the same token simply updates.
    /// </summary>
    public async Task RegisterDeviceAsync()
    {
        try
        {
            var device = await _push.GetDeviceAsync();
            if (device is null) return;

            var language = _loc.CurrentCode;
            if (device.Token == _registeredToken && language == _registeredLanguage) return;

            var ok = await _api.RegisterPushDeviceAsync(new RegisterDeviceRequest
            {
                Platform = device.Platform,
                Token = device.Token,
                DeviceModel = device.DeviceModel,
                AppVersion = device.AppVersion,
                TimeZone = device.TimeZone,
                Language = language
            });

            if (!ok) return;

            _registeredToken = device.Token;
            _registeredLanguage = language;

            // Android 13 and later show nothing until the seeker allows it. Asked here, once there is
            // something that would actually arrive, rather than on first launch with no context.
            if (Settings is { MasterEnabled: true } settings && settings.Items.Any(i => i.IsEnabled))
                await _reminders.IsAllowedAsync();
        }
        catch
        {
            // Push is an enhancement. A phone that cannot register keeps its own alarms.
        }
    }

    /// <summary>
    /// Hands the current preferences to the OS scheduler.
    /// </summary>
    /// <remarks>
    /// Only ever called with settings that were actually fetched. An empty plan means "cancel
    /// everything", so syncing after a FAILED fetch would silently delete the seeker's reminders —
    /// opening the notification panel once with no signal would leave them with no reminders at
    /// all, and nothing on screen would say so.
    /// </remarks>
    public async Task SyncRemindersAsync()
    {
        if (Settings is null) return;

        try { await _reminders.SyncAsync(Settings, PushDelivers, LabelOrNull); }
        catch { /* scheduling is best-effort; never break the panel over it */ }
    }

    /// <summary>Takes freshly saved settings (from the My Notifications screen) and reschedules from them.</summary>
    public async Task UseAsync(MyNotificationSettings settings)
    {
        Settings = settings;
        OnChanged?.Invoke();
        await RegisterDeviceAsync();   // the language may have changed since the last registration
        await SyncRemindersAsync();
    }

    /// <summary>The label in the seeker's language, or null when the bundle has none (the key comes back unchanged).</summary>
    private string? LabelOrNull(string key)
    {
        var text = _loc.T(key, string.Empty);

        // A label with a {placeholder} needs figures only the server has (the streak, a time);
        // shown raw it would read as a bug, so the plain wording is used instead.
        return string.IsNullOrWhiteSpace(text) || text == key || text.Contains('{') ? null : text;
    }

    /// <summary>Refetches the inbox and its unread count. Best-effort, like the badge it feeds.</summary>
    public async Task RefreshInboxAsync()
    {
        try
        {
            var inbox = await _api.GetNotificationInboxAsync();
            if (inbox is null) return;

            Inbox = inbox.Items;
            Unread = inbox.Unread;
            OnChanged?.Invoke();
        }
        catch
        {
            // Keep whatever was there.
        }
    }

    /// <summary>
    /// Drops the previous seeker's settings, inbox and scheduled reminders on sign-out, and takes
    /// this phone off the server's push list — otherwise one account's 06:00 sadhana reminder would
    /// keep arriving on the next account's phone.
    /// </summary>
    public void Reset()
    {
        var token = _registeredToken;

        Settings = null;
        Inbox = Array.Empty<InboxItem>();
        Unread = 0;
        IsOpen = false;
        Error = null;
        _registeredToken = null;
        _registeredLanguage = null;

        _ = _reminders.ClearAsync();

        // The endpoint needs no session, so this works even when the sign-out is an expired one.
        if (token is not null) _ = RemoveDeviceQuietlyAsync(token);

        OnChanged?.Invoke();
    }

    private async Task RemoveDeviceQuietlyAsync(string token)
    {
        try { await _api.RemovePushDeviceAsync(token); }
        catch { /* offline at sign-out: the next sign-in on this phone reassigns the token anyway */ }
    }

    /// <summary>
    /// Opens the panel and refetches. "Active right now" is time-dependent, so a value cached at
    /// start-up goes stale the moment a window opens or closes.
    /// </summary>
    public async Task OpenAsync()
    {
        IsOpen = true;
        IsLoading = true;
        Error = null;
        OnChanged?.Invoke();

        try
        {
            Settings = await _api.GetMyNotificationsAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnChanged?.Invoke();
        }

        // Only when the refetch actually produced settings — see SyncRemindersAsync.
        if (Error is null) await SyncRemindersAsync();

        await RefreshInboxAsync();

        // Seen is seen: the badge clears now, while the rows keep their "new" mark until the panel
        // is next opened, so the seeker can tell which ones they had not read.
        if (Unread > 0)
        {
            Unread = 0;
            OnChanged?.Invoke();
            try { await _api.MarkNotificationInboxReadAsync(); }
            catch { /* it will be marked the next time the panel opens */ }
        }
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        OnChanged?.Invoke();
    }
}

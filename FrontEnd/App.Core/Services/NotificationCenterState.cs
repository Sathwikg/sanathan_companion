using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// Shared state for the mobile notification centre: the settings behind the badge, and whether the
/// panel is open.
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
public class NotificationCenterState
{
    private readonly IApiClient _api;

    public NotificationCenterState(IApiClient api) => _api = api;

    public MyNotificationSettings? Settings { get; private set; }
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
        }
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
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        OnChanged?.Invoke();
    }
}

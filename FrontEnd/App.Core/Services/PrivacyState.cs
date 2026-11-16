using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// The signed-in seeker's privacy choices, fetched once per session and shared by the consent
/// screen, the profile and the page-visit tracker. Reset on sign-in and sign-out so the next
/// account never inherits the previous one's consent.
/// </summary>
/// <remarks>
/// A failed fetch leaves <see cref="Current"/> null, which every reader treats as "no optional
/// consent" and "nothing to ask": an outage must never record page visits, and must never lock a
/// seeker out behind a consent screen that cannot be answered.
/// </remarks>
public sealed class PrivacyState : IUserSessionState
{
    private readonly IApiClient _api;
    private Task<PrivacySettings?>? _loading;

    public PrivacyState(IApiClient api) => _api = api;

    public PrivacySettings? Current { get; private set; }

    public event Action? Changed;

    /// <summary>The seeker switched usage statistics on (DPDP Act, section 6: optional, opt-in).</summary>
    public bool AllowsUsageAnalytics => Current?.UsageAnalytics == true;

    /// <summary>The notice changed, or the account predates it, so the seeker must accept it again.</summary>
    public bool NoticeUpdateRequired => Current?.NoticeUpdateRequired == true;

    /// <summary>Fetches once and shares the answer; <paramref name="force"/> fetches again.</summary>
    public Task<PrivacySettings?> LoadAsync(bool force = false)
    {
        if (!force && Current is not null) return Task.FromResult<PrivacySettings?>(Current);
        if (!force && _loading is not null) return _loading;
        return _loading = FetchAsync();
    }

    /// <summary>Takes the server's answer to a change the seeker just made.</summary>
    public void Set(PrivacySettings settings)
    {
        Current = settings;
        Changed?.Invoke();
    }

    public void Reset()
    {
        Current = null;
        _loading = null;
        Changed?.Invoke();
    }

    private async Task<PrivacySettings?> FetchAsync()
    {
        try
        {
            var settings = await _api.GetPrivacyAsync();
            if (settings is not null) Set(settings);
            return settings;
        }
        catch
        {
            // Offline, or a server without the endpoint yet: ask nothing and record nothing.
            return null;
        }
        finally
        {
            _loading = null;
        }
    }
}

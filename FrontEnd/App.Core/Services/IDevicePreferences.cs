namespace App.Core.Services;

/// <summary>
/// Small on/off choices that belong to this device rather than to the account
/// (web = localStorage, mobile = Preferences).
/// </summary>
/// <remarks>
/// For settings about the phone in the seeker's hand — the japa screen's Battery Saver — which
/// should not follow them to another device the way their mala size does.
/// </remarks>
public interface IDevicePreferences
{
    Task<bool> GetFlagAsync(string key, bool fallback = false);

    Task SetFlagAsync(string key, bool value);
}

/// <summary>Keys for <see cref="IDevicePreferences"/>, kept in one place so two screens never collide.</summary>
public static class DevicePreferenceKeys
{
    public const string JapaBatterySaver = "sc-japa-lite";
}

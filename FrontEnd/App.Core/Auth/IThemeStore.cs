namespace App.Core.Auth;

/// <summary>Per-platform theme persistence (web = localStorage, mobile = Preferences).</summary>
public interface IThemeStore
{
    Task<string?> GetThemeAsync();
    Task SetThemeAsync(string theme);
}

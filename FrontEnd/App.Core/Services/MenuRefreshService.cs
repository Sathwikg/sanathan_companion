namespace App.Core.Services;

/// <summary>Lightweight app-wide signal so the sidebar can refresh after menu edits.</summary>
public class MenuRefreshService
{
    public event Action? OnChanged;

    public void NotifyChanged() => OnChanged?.Invoke();
}

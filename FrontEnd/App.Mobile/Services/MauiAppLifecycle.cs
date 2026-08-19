using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// Reports whether the app window is in front, so shared components can idle while it is not.
/// </summary>
/// <remarks>
/// Fed by App.xaml.cs from the window's Activated/Deactivated events rather than reading any state
/// itself — MAUI surfaces the lifecycle on the Window, which is created long after the DI container
/// is built, so the flow has to be push, not pull.
/// </remarks>
public class MauiAppLifecycle : IAppLifecycle
{
    private bool _isActive = true;

    public bool IsActive => _isActive;

    public event Action? Changed;

    internal void SetActive(bool isActive)
    {
        if (_isActive == isActive) return;

        _isActive = isActive;
        Changed?.Invoke();
    }
}

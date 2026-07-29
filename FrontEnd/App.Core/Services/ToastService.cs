namespace App.Core.Services;

public enum ToastKind { Success, Error, Info, Warning }

public class ToastMessage
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Message { get; set; } = string.Empty;
    public string? Title { get; set; }
    public ToastKind Kind { get; set; } = ToastKind.Success;
    public int DurationMs { get; set; } = 3200;
}

/// <summary>Lightweight toast/alert notifications. A host component renders the stack.</summary>
public class ToastService
{
    public event Action<ToastMessage>? Shown;

    public void Show(ToastMessage toast) => Shown?.Invoke(toast);

    public void Success(string message, string? title = null)
        => Show(new ToastMessage { Message = message, Title = title, Kind = ToastKind.Success });

    public void Error(string message, string? title = null)
        => Show(new ToastMessage { Message = message, Title = title, Kind = ToastKind.Error, DurationMs = 4500 });

    public void Info(string message, string? title = null)
        => Show(new ToastMessage { Message = message, Title = title, Kind = ToastKind.Info });

    public void Warning(string message, string? title = null)
        => Show(new ToastMessage { Message = message, Title = title, Kind = ToastKind.Warning });
}

namespace App.Core.Services;

public enum ConfirmKind { Question, Warning, Danger, Success, Info }

public class ConfirmRequest
{
    public string Title { get; set; } = "Are you sure?";
    public string Message { get; set; } = string.Empty;
    public string ConfirmText { get; set; } = "Yes";
    public string CancelText { get; set; } = "Cancel";
    public ConfirmKind Kind { get; set; } = ConfirmKind.Question;
    public string? Icon { get; set; }

    internal TaskCompletionSource<bool> Completion { get; } = new();
}

/// <summary>Sweet-alert style confirmation dialog service. A host component renders the dialog.</summary>
public class ConfirmService
{
    public event Func<ConfirmRequest, Task>? Requested;

    public async Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        if (Requested is null) return true;
        await Requested.Invoke(request);
        return await request.Completion.Task;
    }

    /// <summary>Called by the dialog host to resolve the pending confirmation.</summary>
    public void Resolve(ConfirmRequest request, bool result) => request.Completion.TrySetResult(result);

    public Task<bool> ConfirmAsync(string title, string message,
        string confirmText = "Yes", string cancelText = "Cancel",
        ConfirmKind kind = ConfirmKind.Question, string? icon = null)
        => ConfirmAsync(new ConfirmRequest
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText,
            CancelText = cancelText,
            Kind = kind,
            Icon = icon
        });
}

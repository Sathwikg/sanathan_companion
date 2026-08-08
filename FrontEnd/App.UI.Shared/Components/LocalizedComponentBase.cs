using App.Core.Services;
using Microsoft.AspNetCore.Components;

namespace App.UI.Shared.Components;

/// <summary>
/// Base for any page or component that shows translated text. Inheriting it gives you a
/// <see cref="Loc"/> property and an automatic re-render when the user switches language, so a
/// page only has to write <c>@Loc["common.save", "Save"]</c>.
/// </summary>
/// <remarks>
/// Always pass the English text as the second argument. It is the fallback when a key has no
/// translation yet, which keeps a half-translated language perfectly readable instead of leaking
/// raw keys like "common.save" into the UI.
/// </remarks>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected LocalizationState Loc { get; set; } = default!;

    private bool _subscribed;

    protected override async Task OnInitializedAsync()
    {
        if (!_subscribed)
        {
            Loc.OnChanged += OnLanguageChanged;
            _subscribed = true;
        }
        await Loc.EnsureLoadedAsync();
        await base.OnInitializedAsync();
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        if (_subscribed)
        {
            Loc.OnChanged -= OnLanguageChanged;
            _subscribed = false;
        }
        GC.SuppressFinalize(this);
    }
}

using System.Text.Json;
using Microsoft.JSInterop;

namespace App.Core.Services;

/// <summary>
/// Carries the ticket from a first Google sign-in to the page that finishes it — the registration
/// form for a new address, or the password prompt for an existing account.
/// </summary>
/// <remarks>
/// Scoped, and mirrored to sessionStorage so a reload of the page that finishes the job does not
/// drop it. Seekers on a phone reload, or the browser does it for them after the Google tab
/// closes, and the form came back empty with no way to continue except signing in again.
/// sessionStorage rather than localStorage or a query string: it belongs to this one tab, dies
/// with it, and never lands in history or logs. The ticket itself expires within minutes; a page
/// that finds it empty or expired sends the seeker back to the login screen to start again.
/// </remarks>
public class GoogleSignInState
{
    private const string StorageKey = "sc.googleSignIn";

    private readonly IJSRuntime _js;
    private bool _restored;

    public GoogleSignInState(IJSRuntime js) => _js = js;

    public string? Ticket { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string? Email { get; private set; }
    public string? FullName { get; private set; }

    /// <summary>Which page finishes the job: one of <c>GoogleOutcomes</c>.</summary>
    public string? Outcome { get; private set; }

    public bool IsUsable => !string.IsNullOrWhiteSpace(Ticket) && ExpiresAtUtc > DateTime.UtcNow;

    public async Task SetAsync(string ticket, DateTime? expiresAtUtc, string? email, string? fullName, string outcome)
    {
        Ticket = ticket;
        // Absent from the response means "trust the server's default" rather than "already dead".
        ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(10);
        Email = email;
        FullName = fullName;
        Outcome = outcome;
        _restored = true;

        var saved = new Saved(Ticket, ExpiresAtUtc.Value, Email, FullName, Outcome);
        try { await _js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, JsonSerializer.Serialize(saved)); }
        catch { /* storage unavailable: the in-memory copy still finishes the job without a reload */ }
    }

    public async Task ClearAsync()
    {
        Ticket = null;
        ExpiresAtUtc = null;
        Email = null;
        FullName = null;
        Outcome = null;
        _restored = true;

        try { await _js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey); }
        catch { /* nothing was stored, or storage is unavailable */ }
    }

    /// <summary>
    /// Picks up a ticket this tab saved before a reload. Only the first call reads storage, and an
    /// expired copy is discarded rather than restored.
    /// </summary>
    public async Task RestoreAsync()
    {
        if (_restored) return;
        _restored = true;

        try
        {
            var json = await _js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(json)) return;

            var saved = JsonSerializer.Deserialize<Saved>(json);
            if (saved is null || string.IsNullOrWhiteSpace(saved.Ticket) || saved.ExpiresAtUtc <= DateTime.UtcNow)
            {
                await _js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
                return;
            }

            Ticket = saved.Ticket;
            ExpiresAtUtc = saved.ExpiresAtUtc;
            Email = saved.Email;
            FullName = saved.FullName;
            Outcome = saved.Outcome;
        }
        catch
        {
            // Unreadable or unavailable storage: behave as before, with nothing to restore.
        }
    }

    private sealed record Saved(string Ticket, DateTime ExpiresAtUtc, string? Email, string? FullName, string? Outcome);
}

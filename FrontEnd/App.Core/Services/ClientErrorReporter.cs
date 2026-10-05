using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// Sends client-side crashes to the server's error log.
/// </summary>
/// <remarks>
/// <para>
/// Guarded on both sides of the wire: a crash that repeats on every render is sent once a minute,
/// not hundreds of times, and no more than <see cref="MaxReportsPerSession"/> reports leave one app
/// session at all, so a broken page cannot exhaust the server's rate limit for everyone behind the
/// same address.
/// </para>
/// <para>
/// A native host can also hand over a crash that killed the previous run through
/// <see cref="ICrashStore"/>; it is sent on the next start.
/// </para>
/// </remarks>
public sealed class ClientErrorReporter
{
    public const int MaxReportsPerSession = 20;
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(1);

    private readonly IApiClient _api;
    private readonly ICrashStore _crashStore;
    private readonly Dictionary<string, DateTime> _recent = new();
    private readonly object _gate = new();
    private int _sent;
    private bool _pendingChecked;

    public ClientErrorReporter(IApiClient api, ICrashStore crashStore)
    {
        _api = api;
        _crashStore = crashStore;
    }

    /// <summary>Reports an exception. Never throws, and never waits for the network.</summary>
    public void Report(Exception exception, string? route, string severity = "Error")
    {
        var report = new LogErrorRequestModel
        {
            Source = ClientPlatform.ErrorSource,
            Severity = severity,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message,
            StackTrace = exception.ToString(),
            InnerException = exception.InnerException?.Message,
            RequestPath = route
        };

        if (!ShouldSend(report)) return;
        _ = SendAsync(report);
    }

    /// <summary>Sends the crash a native host saved before the previous run died, once.</summary>
    public async Task SendPendingCrashAsync()
    {
        if (_pendingChecked) return;
        _pendingChecked = true;

        try
        {
            var pending = await _crashStore.TakeAsync();
            if (pending is not null && ShouldSend(pending))
                await _api.LogErrorAsync(pending);
        }
        catch
        {
            // Diagnostics must never become the problem.
        }
    }

    private bool ShouldSend(LogErrorRequestModel report)
    {
        var key = $"{report.ExceptionType}|{report.Message}|{report.RequestPath}";
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            if (_sent >= MaxReportsPerSession) return false;
            if (_recent.TryGetValue(key, out var last) && now - last < RepeatWindow) return false;

            _recent[key] = now;
            _sent++;
            return true;
        }
    }

    private async Task SendAsync(LogErrorRequestModel report)
    {
        try { await _api.LogErrorAsync(report); }
        catch { /* fire and forget */ }
    }
}

/// <summary>
/// Where a native host parks a crash that is about to kill the process, so it can be reported on
/// the next start. The browser has no such moment, so its implementation holds nothing.
/// </summary>
public interface ICrashStore
{
    /// <summary>Returns the saved crash, if any, and forgets it.</summary>
    Task<LogErrorRequestModel?> TakeAsync();
}

public sealed class NoCrashStore : ICrashStore
{
    public Task<LogErrorRequestModel?> TakeAsync() => Task.FromResult<LogErrorRequestModel?>(null);
}

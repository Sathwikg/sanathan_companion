using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Notifications;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Services.BackgroundJobs;

/// <summary>
/// The recurring job that wakes the notification engine once a minute.
/// </summary>
/// <remarks>
/// Every minute rather than every five because the things it announces are about a minute: a
/// reminder set for 06:00 should arrive at 06:00, and "Rahu Kalam begins in ten minutes" should not
/// mean anything between five and ten. A minute with nothing due costs one small query, and a
/// server with push switched off costs none.
/// </remarks>
public sealed class NotificationEngineJob
{
    /// <summary>The recurring job's id in Hangfire; the console reads its last and next execution by this name.</summary>
    public const string RecurringJobId = "notification-engine";

    // One pass at a time in this process. A pass that overruns its minute (a very large audience,
    // a slow push service) must not be joined by the next; the dedupe key would keep the two from
    // double-sending, but they would still do all the work twice.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly INotificationEngine _engine;
    private readonly ILogger<NotificationEngineJob> _logger;

    public NotificationEngineJob(INotificationEngine engine, ILogger<NotificationEngineJob> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>One scheduled pass. Returns a line Hangfire shows as the job's result.</summary>
    public async Task<string> TickAsync(CancellationToken cancellationToken = default)
    {
        if (!await Gate.WaitAsync(0, cancellationToken))
            return "Skipped: the previous pass is still running.";

        try
        {
            var summary = await _engine.RunAsync(new EngineRunOptions(Trigger: NotificationTriggers.Schedule), cancellationToken);

            return summary.Ran
                ? $"{summary.Due} due, {summary.Sent} sent, {summary.Failed} failed ({summary.Audience} reachable)"
                : $"Idle: {summary.SkippedReason}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification engine pass failed");
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }
}

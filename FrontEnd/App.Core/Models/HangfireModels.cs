namespace App.Core.Models;

/// <summary>
/// Hangfire statistics DTO.
/// </summary>
public sealed record HangfireStatsDto(
    long Enqueued,
    long Processing,
    long Succeeded,
    long Failed,
    long Deleted,
    long Scheduled,
    long Recurring,
    long Queues
);

/// <summary>
/// Recurring job DTO.
/// </summary>
public sealed record RecurringJobDto(
    string Id,
    string Cron,
    string Queue,
    DateTime? LastExecution,
    DateTime? NextExecution,
    string? JobType,
    string? JobMethod
);

/// <summary>
/// Failed job DTO.
/// </summary>
public sealed record FailedJobDto(
    string Id,
    string? JobType,
    string? Method,
    string? ExceptionType,
    string? ExceptionMessage,
    DateTime? FailedAt,
    string? Queue
);
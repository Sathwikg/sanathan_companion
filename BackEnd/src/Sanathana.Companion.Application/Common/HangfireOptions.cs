using System.ComponentModel.DataAnnotations;

namespace Sanathana.Companion.Application.Common;

/// <summary>
/// Configuration for Hangfire background job processing.
/// </summary>
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Enables Hangfire background job processing. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Connection string for Hangfire PostgreSQL storage. If not set, uses the main DefaultConnection.
    /// </summary>
    public string? StorageConnectionString { get; set; }

    /// <summary>
    /// The storage connection string in force: the override when one is set, else the application's
    /// own DefaultConnection. Blank and null both mean "none set"; the shipped appsettings.json uses "".
    /// </summary>
    public static string? ResolveConnectionString(HangfireOptions options, string? defaultConnection)
        => string.IsNullOrWhiteSpace(options.StorageConnectionString) ? defaultConnection : options.StorageConnectionString;

    /// <summary>
    /// Schema name for Hangfire tables. Default: "hangfire".
    /// </summary>
    public string SchemaName { get; set; } = "hangfire";

    /// <summary>
    /// Number of worker threads for background job processing. Default: Environment.ProcessorCount.
    /// </summary>
    public int? WorkerCount { get; set; }

    /// <summary>
    /// Queue polling interval in seconds. Default: 15.
    /// </summary>
    public int QueuePollIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Job expiration timeout in days. Default: 7.
    /// </summary>
    public int JobExpirationTimeoutDays { get; set; } = 7;

    /// <summary>
    /// Enables the Hangfire Dashboard. Default: true in Development, false in Production.
    /// </summary>
    public bool? DashboardEnabled { get; set; }

    /// <summary>
    /// Dashboard path. Default: "/hangfire".
    /// </summary>
    public string DashboardPath { get; set; } = "/hangfire";

    /// <summary>
    /// Requires Admin role for dashboard access. Default: true.
    /// </summary>
    public bool DashboardRequireAdmin { get; set; } = true;

    /// <summary>
    /// Automatic retry attempts for failed jobs. Default: 3.
    /// </summary>
    public int AutomaticRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Timeout for job execution in minutes. Default: 30.
    /// </summary>
    public int JobExecutionTimeoutMinutes { get; set; } = 30;
}
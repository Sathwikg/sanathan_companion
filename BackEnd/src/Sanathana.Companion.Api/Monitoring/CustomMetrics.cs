using System.Diagnostics.Metrics;

namespace Sanathana.Companion.Api.Monitoring;

/// <summary>
/// Custom metrics for Sanathana Companion application.
/// These metrics are exposed via Prometheus for alerting.
/// </summary>
public static class CustomMetrics
{
    public static readonly Meter Meter = new("Sanathana.Companion", "1.0.0");

    // HTTP Request Metrics
    public static readonly Counter<long> HttpRequestsTotal = Meter.CreateCounter<long>(
        "http_requests_total",
        "Total number of HTTP requests",
        "requests");

    public static readonly Counter<long> HttpRequests5xxTotal = Meter.CreateCounter<long>(
        "http_requests_5xx_total",
        "Total number of 5xx HTTP responses",
        "requests");

    public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>(
        "http_request_duration_seconds",
        "HTTP request duration in seconds",
        "seconds");

    // Authentication Metrics
    public static readonly Counter<long> AuthAttemptsTotal = Meter.CreateCounter<long>(
        "auth_attempts_total",
        "Total number of authentication attempts",
        "attempts");

    public static readonly Counter<long> AuthFailuresTotal = Meter.CreateCounter<long>(
        "auth_failures_total",
        "Total number of authentication failures",
        "attempts");

    public static readonly Counter<long> AuthSuccessTotal = Meter.CreateCounter<long>(
        "auth_success_total",
        "Total number of successful authentications",
        "attempts");

    public static readonly Counter<long> TokenRefreshTotal = Meter.CreateCounter<long>(
        "token_refresh_total",
        "Total number of token refresh attempts",
        "attempts");

    public static readonly Counter<long> TokenRefreshFailuresTotal = Meter.CreateCounter<long>(
        "token_refresh_failures_total",
        "Total number of token refresh failures",
        "attempts");

    // Database Connection Pool Metrics
    public static readonly UpDownCounter<long> DbConnectionsActive = Meter.CreateUpDownCounter<long>(
        "db_connections_active",
        "Number of active database connections",
        "connections");

    public static readonly Counter<long> DbConnectionPoolExhaustedTotal = Meter.CreateCounter<long>(
        "db_connection_pool_exhausted_total",
        "Total number of times connection pool was exhausted",
        "events");

    public static readonly Histogram<double> DbQueryDuration = Meter.CreateHistogram<double>(
        "db_query_duration_seconds",
        "Database query duration in seconds",
        "seconds");

    // Migration Metrics
    public static readonly Counter<long> MigrationAppliedTotal = Meter.CreateCounter<long>(
        "migration_applied_total",
        "Total number of migrations applied",
        "migrations");

    public static readonly Counter<long> MigrationFailedTotal = Meter.CreateCounter<long>(
        "migration_failed_total",
        "Total number of migration failures",
        "migrations");

    public static readonly Gauge<int> PendingMigrations = Meter.CreateGauge<int>(
        "pending_migrations",
        "Number of pending database migrations");

    // Background Job Metrics
    public static readonly Counter<long> BackgroundJobsEnqueuedTotal = Meter.CreateCounter<long>(
        "background_jobs_enqueued_total",
        "Total number of background jobs enqueued",
        "jobs");

    public static readonly Counter<long> BackgroundJobsSucceededTotal = Meter.CreateCounter<long>(
        "background_jobs_succeeded_total",
        "Total number of background jobs succeeded",
        "jobs");

    public static readonly Counter<long> BackgroundJobsFailedTotal = Meter.CreateCounter<long>(
        "background_jobs_failed_total",
        "Total number of background jobs failed",
        "jobs");

    public static readonly Histogram<double> BackgroundJobDuration = Meter.CreateHistogram<double>(
        "background_job_duration_seconds",
        "Background job execution duration in seconds",
        "seconds");

    // Business Metrics
    public static readonly Counter<long> UsersRegisteredTotal = Meter.CreateCounter<long>(
        "users_registered_total",
        "Total number of user registrations",
        "users");

    public static readonly Counter<long> ActiveUsers = Meter.CreateCounter<long>(
        "active_users_total",
        "Total number of active users (daily)",
        "users");

    public static readonly Counter<long> PanchangamComputationsTotal = Meter.CreateCounter<long>(
        "panchangam_computations_total",
        "Total number of panchangam computations",
        "computations");

    public static readonly Histogram<double> PanchangamComputeDuration = Meter.CreateHistogram<double>(
        "panchangam_compute_duration_seconds",
        "Panchangam computation duration in seconds",
        "seconds");
}
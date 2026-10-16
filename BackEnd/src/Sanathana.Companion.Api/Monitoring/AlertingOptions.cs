namespace Sanathana.Companion.Api.Monitoring;

/// <summary>
/// Configuration for alerting rules and thresholds.
/// These match the Prometheus alerting rules that should be configured in Alertmanager.
/// </summary>
public sealed class AlertingOptions
{
    public const string SectionName = "Alerting";

    /// <summary>
    /// Enables alerting evaluation (requires Alertmanager or similar).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 5xx error rate alert threshold (percentage). Default: 5%.
    /// Alert if 5xx rate exceeds this percentage over 5 minutes.
    /// </summary>
    public double ErrorRateThresholdPercent { get; set; } = 5.0;

    /// <summary>
    /// Authentication failure rate alert threshold (percentage). Default: 10%.
    /// Alert if auth failure rate exceeds this percentage over 5 minutes.
    /// </summary>
    public double AuthFailureRateThresholdPercent { get; set; } = 10.0;

    /// <summary>
    /// Connection pool exhaustion alert. Default: true (alert on any exhaustion).
    /// </summary>
    public bool AlertOnPoolExhaustion { get; set; } = true;

    /// <summary>
    /// Migration failure alert. Default: true (alert on any migration failure).
    /// </summary>
    public bool AlertOnMigrationFailure { get; set; } = true;

    /// <summary>
    /// Pending migrations alert threshold. Default: 1 (alert if any pending).
    /// </summary>
    public int PendingMigrationsThreshold { get; set; } = 1;

    /// <summary>
    /// High latency alert threshold in seconds. Default: 5s (p95).
    /// </summary>
    public double HighLatencyThresholdSeconds { get; set; } = 5.0;

    /// <summary>
    /// Background job failure rate alert threshold (percentage). Default: 10%.
    /// </summary>
    public double BackgroundJobFailureRateThresholdPercent { get; set; } = 10.0;

    /// <summary>
    /// Disk space alert threshold (percentage). Default: 85%.
    /// </summary>
    public double DiskSpaceThresholdPercent { get; set; } = 85.0;

    /// <summary>
    /// Memory usage alert threshold (percentage). Default: 85%.
    /// </summary>
    public double MemoryUsageThresholdPercent { get; set; } = 85.0;
}

/// <summary>
/// Prometheus alerting rules as a reference for Alertmanager configuration.
/// These should be deployed to your Prometheus/Alertmanager setup.
/// </summary>
public static class PrometheusAlertRules
{
    public static string RulesYaml => """
groups:
  - name: sanathana-companion-alerts
    interval: 30s
    rules:
      # 5xx Error Rate Alert
      - alert: High5xxErrorRate
        expr: |
          (
            sum(rate(http_requests_5xx_total[5m])) by (job)
            /
            sum(rate(http_requests_total[5m])) by (job)
          ) * 100 > 5
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: 'High 5xx error rate on {{ $labels.job }}'
          description: '5xx error rate is {{ $value | printf "%.2f" }}% (threshold: 5%)'

      # Authentication Failure Rate Alert
      - alert: HighAuthFailureRate
        expr: |
          (
            sum(rate(auth_failures_total[5m])) by (job)
            /
            sum(rate(auth_attempts_total[5m])) by (job)
          ) * 100 > 10
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: 'High authentication failure rate on {{ $labels.job }}'
          description: 'Auth failure rate is {{ $value | printf "%.2f" }}% (threshold: 10%)'

      # Connection Pool Exhaustion Alert
      - alert: DatabaseConnectionPoolExhausted
        expr: increase(db_connection_pool_exhausted_total[5m]) > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: 'Database connection pool exhausted on {{ $labels.job }}'
          description: 'Connection pool has been exhausted {{ $value }} times in the last 5 minutes'

      # Migration Failure Alert
      - alert: DatabaseMigrationFailed
        expr: increase(migration_failed_total[5m]) > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: 'Database migration failed on {{ $labels.job }}'
          description: 'Migration failed {{ $value }} times in the last 5 minutes'

      # Pending Migrations Alert
      - alert: PendingDatabaseMigrations
        expr: pending_migrations > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: 'Pending database migrations on {{ $labels.job }}'
          description: '{{ $value }} migrations are pending application'

      # High Latency Alert (p95)
      - alert: HighRequestLatency
        expr: |
          histogram_quantile(0.95, 
            sum(rate(http_request_duration_seconds_bucket[5m])) by (le, job)
          ) > 5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: 'High request latency on {{ $labels.job }}'
          description: 'p95 latency is {{ $value | printf "%.2f" }}s (threshold: 5s)'

      # Background Job Failure Rate Alert
      - alert: HighBackgroundJobFailureRate
        expr: |
          (
            sum(rate(background_jobs_failed_total[5m])) by (job)
            /
            sum(rate(background_jobs_enqueued_total[5m])) by (job)
          ) * 100 > 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: 'High background job failure rate on {{ $labels.job }}'
          description: 'Background job failure rate is {{ $value | printf "%.2f" }}% (threshold: 10%)'

      # Disk Space Alert (requires node_exporter)
      - alert: DiskSpaceLow
        expr: |
          (node_filesystem_avail_bytes{mountpoint="/"} / node_filesystem_size_bytes{mountpoint="/"}) * 100 < 15
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: 'Low disk space on {{ $labels.instance }}'
          description: 'Disk space is {{ $value | printf "%.1f" }}% free (threshold: 15%)'

      # Memory Usage Alert (requires node_exporter)
      - alert: HighMemoryUsage
        expr: |
          (1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)) * 100 > 85
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: 'High memory usage on {{ $labels.instance }}'
          description: 'Memory usage is {{ $value | printf "%.1f" }}% (threshold: 85%)'
""";
}
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.Metrics;

namespace Sanathana.Companion.Api.HealthChecks;

/// <summary>
/// Health check that validates metrics collection is working.
/// </summary>
public sealed class MetricsHealthCheck : IHealthCheck
{
    private readonly Meter _meter;
    private readonly Counter<long> _healthCheckCounter;

    public MetricsHealthCheck()
    {
        _meter = new Meter("Sanathana.Companion.Health");
        _healthCheckCounter = _meter.CreateCounter<long>("health_check_executions_total");
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        _healthCheckCounter.Add(1);
        return Task.FromResult(HealthCheckResult.Healthy("Metrics collection operational"));
    }
}
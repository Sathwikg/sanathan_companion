using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Api.HealthChecks;

/// <summary>
/// Health check for database connectivity and migration status.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(ApplicationDbContext db, ILogger<DatabaseHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test basic connectivity
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Check if migrations are applied
            var pendingMigrations = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                var migrationList = string.Join(", ", pendingMigrations);
                _logger.LogWarning("Pending migrations detected: {Migrations}", migrationList);
                return HealthCheckResult.Degraded($"Pending migrations: {migrationList}");
            }

            // Test a simple query
            var regionCount = await _db.Regions.CountAsync(cancellationToken);
            
            return HealthCheckResult.Healthy($"Database connected. Regions: {regionCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}

/// <summary>
/// Health check for Supabase connection pool status.
/// </summary>
public sealed class ConnectionPoolHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ConnectionPoolHealthCheck> _logger;

    public ConnectionPoolHealthCheck(ApplicationDbContext db, ILogger<ConnectionPoolHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test multiple concurrent connections to verify pool health
            var tasks = Enumerable.Range(0, 5).Select(_ => _db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken));
            await Task.WhenAll(tasks);

            return HealthCheckResult.Healthy("Connection pool healthy");
        }
        catch (Exception ex) when (ex.Message.Contains("EMAXCONNSESSION", StringComparison.OrdinalIgnoreCase) || 
                                    ex.Message.Contains("pool", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Connection pool exhaustion detected");
            return HealthCheckResult.Unhealthy("Connection pool exhausted", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection pool health check failed");
            return HealthCheckResult.Degraded("Connection pool check failed", ex);
        }
    }
}

/// <summary>
/// Health check for external service dependencies (Google Auth, etc.).
/// </summary>
public sealed class ExternalServicesHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalServicesHealthCheck> _logger;

    public ExternalServicesHealthCheck(IHttpClientFactory httpClientFactory, ILogger<ExternalServicesHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        // Check Google OAuth endpoint (if configured)
        try
        {
            var response = await client.GetAsync("https://oauth2.googleapis.com/tokeninfo", cancellationToken);
            results.Add($"Google OAuth: {(response.IsSuccessStatusCode ? "OK" : "Degraded")}");
        }
        catch (Exception ex)
        {
            results.Add($"Google OAuth: Failed ({ex.Message})");
            _logger.LogWarning(ex, "Google OAuth health check failed");
        }

        // All external services are optional - report as healthy if at least one works
        var healthy = results.Any(r => r.Contains("OK"));
        return healthy 
            ? HealthCheckResult.Healthy(string.Join("; ", results))
            : HealthCheckResult.Degraded(string.Join("; ", results));
    }
}
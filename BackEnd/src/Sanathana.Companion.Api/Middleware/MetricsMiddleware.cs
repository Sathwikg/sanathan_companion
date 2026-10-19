using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Sanathana.Companion.Api.Monitoring;

namespace Sanathana.Companion.Api.Middleware;

/// <summary>
/// Middleware for collecting HTTP request metrics for Prometheus.
/// </summary>
public sealed class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Meter _meter = new("Sanathana.Companion.Http");
    private static readonly Counter<long> _requestsTotal = _meter.CreateCounter<long>("http_requests_total", "Total HTTP requests");
    private static readonly Counter<long> _requests5xxTotal = _meter.CreateCounter<long>("http_requests_5xx_total", "Total 5xx HTTP responses");
    private static readonly Histogram<double> _requestDuration = _meter.CreateHistogram<double>("http_request_duration_seconds", "HTTP request duration", "seconds");

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "unknown";
        var method = context.Request.Method;

        try
        {
            await _next(context);
            
            // Record metrics after successful response
            var statusCode = context.Response.StatusCode;
            var is5xx = statusCode >= 500 && statusCode < 600;
            
            _requestsTotal.Add(1, 
                new("method", method), 
                new("path", path), 
                new("status_code", statusCode.ToString()));
            
            if (is5xx)
            {
                _requests5xxTotal.Add(1, 
                    new("method", method), 
                    new("path", path), 
                    new("status_code", statusCode.ToString()));
            }
        }
        catch (Exception)
        {
            // Record 500 for unhandled exceptions
            _requestsTotal.Add(1, 
                new("method", method), 
                new("path", path), 
                new("status_code", "500"));
            _requests5xxTotal.Add(1, 
                new("method", method), 
                new("path", path), 
                new("status_code", "500"));
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _requestDuration.Record(stopwatch.Elapsed.TotalSeconds, 
                new("method", method), 
                new("path", path));
        }
    }
}

/// <summary>
/// Extension method to add metrics middleware.
/// </summary>
public static class MetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}
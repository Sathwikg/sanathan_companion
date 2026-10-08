using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditQueue auditQueue, IAuditConfigCache auditCache,
        ICurrentUserService currentUser, IAuditRequestContext request)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex, auditQueue, auditCache, currentUser, request);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex, IAuditQueue auditQueue, IAuditConfigCache auditCache,
        ICurrentUserService currentUser, IAuditRequestContext request)
    {
        var (statusCode, message) = ex switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest,
                string.Join(" ", ve.Errors.Select(e => e.ErrorMessage))),
            ConflictException => (StatusCodes.Status409Conflict, ex.Message),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            DomainException => (StatusCodes.Status400BadRequest, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "Unhandled exception");

            if (auditCache.TrackErrorLogs)
            {
                // Queued, not written: the request that just failed may have failed because the
                // database is down, and the error log must not add a second failure to it.
                auditQueue.EnqueueError(new ErrorLog
                {
                    TimestampUtc = DateTime.UtcNow,
                    Source = AuditErrorSources.BackendApi,
                    Severity = AuditSeverities.Critical,
                    StatusCode = statusCode,
                    ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = AuditLimits.Cap(ex.Message, AuditLimits.MaxMessage)!,
                    StackTrace = AuditLimits.Cap(ex.ToString(), AuditLimits.MaxStackTrace),
                    InnerException = AuditLimits.Cap(ex.InnerException?.Message, AuditLimits.MaxInnerException),
                    RequestPath = context.Request.Path,
                    RequestMethod = context.Request.Method,
                    UserId = currentUser.UserId,
                    UsernameOrEmail = currentUser.Email,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent
                });
            }
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            statusCode,
            message,
            timestamp = DateTime.UtcNow
        });
    }
}

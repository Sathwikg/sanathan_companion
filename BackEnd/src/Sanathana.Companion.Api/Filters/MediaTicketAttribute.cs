using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Api.Configuration;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Filters;

/// <summary>
/// Requires a valid media ticket in the <c>t</c> query parameter.
/// </summary>
/// <remarks>
/// Sits BESIDE [AllowAnonymous], never instead of it. The authorization middleware runs before MVC
/// filters, so removing [AllowAnonymous] would 401 the request before this filter ever saw it and
/// every ticketed image would break.
/// </remarks>
public sealed class MediaTicketAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        => new MediaTicketFilter(
            serviceProvider.GetRequiredService<IMediaTicketService>(),
            serviceProvider.GetRequiredService<IOptionsMonitor<MediaOptions>>());
}

/// <inheritdoc cref="MediaTicketAttribute" />
public sealed class MediaTicketFilter : IAsyncAuthorizationFilter
{
    private readonly IMediaTicketService _tickets;
    private readonly IOptionsMonitor<MediaOptions> _options;

    public MediaTicketFilter(IMediaTicketService tickets, IOptionsMonitor<MediaOptions> options)
    {
        _tickets = tickets;
        _options = options;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // IOptionsMonitor, not IOptions: this can be turned off without a redeploy, which is what
        // you want at three in the morning when every image on the site has stopped loading.
        if (!_options.CurrentValue.RequireTicket) return Task.CompletedTask;

        var ticket = context.HttpContext.Request.Query["t"].ToString();
        if (_tickets.IsValid(ticket, DateTime.UtcNow)) return Task.CompletedTask;

        context.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentType = "application/json",
            Content = System.Text.Json.JsonSerializer.Serialize(new
            {
                statusCode = StatusCodes.Status401Unauthorized,
                message = "This link has expired.",
                timestamp = DateTime.UtcNow
            })
        };

        return Task.CompletedTask;
    }
}

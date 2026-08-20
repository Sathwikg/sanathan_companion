using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>Hands a signed-in client the ticket its &lt;img&gt; and &lt;audio&gt; elements need.</summary>
[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaTicketService _tickets;

    public MediaController(IMediaTicketService tickets) => _tickets = tickets;

    /// <summary>A ticket for the media endpoints, and when it stops working.</summary>
    /// <remarks>
    /// Exempt from the module gate: every screen that shows a picture needs this, and which
    /// pictures a role may see is decided by the endpoints themselves, not by holding a ticket.
    /// </remarks>
    [ModuleExempt]
    [HttpGet("ticket")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTicket()
    {
        var (ticket, expiresAtUtc) = _tickets.Issue(DateTime.UtcNow);

        // No caching. The client holds it in memory and re-fetches near expiry; a cached ticket
        // would outlive its own validity.
        Response.Headers.CacheControl = "no-store";

        return Ok(new { ticket, expiresAtUtc });
    }
}

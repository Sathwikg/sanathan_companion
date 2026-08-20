using Microsoft.Extensions.Options;
using Sanathana.Companion.Infrastructure.Identity;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The ticket the four byte endpoints require, which turns a media URL from a capability that
/// never expires into one good for this window and the last.
/// </summary>
public class MediaTicketTests
{
    private static MediaTicketService Service(string secret = "sanathana-companion-test-secret-key-0123456789ABCDEF")
        => new(Options.Create(new JwtSettings { Secret = secret }));

    private static readonly DateTime Noon = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_freshly_issued_ticket_is_accepted()
    {
        var service = Service();
        var (ticket, _) = service.Issue(Noon);

        Assert.True(service.IsValid(ticket, Noon));
    }

    [Fact]
    public void It_is_the_same_ticket_for_everyone_in_a_window()
    {
        // This is what keeps the URL cacheable. A per-caller ticket would make every image a
        // separate cache entry for every seeker.
        var service = Service();

        Assert.Equal(service.Issue(Noon).Ticket, service.Issue(Noon.AddMinutes(59)).Ticket);
    }

    [Fact]
    public void A_ticket_never_dies_the_moment_after_it_is_issued()
    {
        // The trap a single-window scheme walks into: a ticket minted at 05:59 into a six-hour
        // window would expire a minute later, and the client would hand the browser a dead ticket
        // it had only just fetched.
        var service = Service();
        var lateInWindow = Noon.AddHours(5).AddMinutes(59);

        var (ticket, expiresAt) = service.Issue(lateInWindow);

        Assert.True(service.IsValid(ticket, lateInWindow.AddMinutes(30)));
        Assert.True(expiresAt - lateInWindow > TimeSpan.FromHours(6));
    }

    [Fact]
    public void The_previous_window_is_still_accepted_but_the_one_before_it_is_not()
    {
        var service = Service();
        var (ticket, _) = service.Issue(Noon);

        Assert.True(service.IsValid(ticket, Noon.AddHours(6)));
        Assert.False(service.IsValid(ticket, Noon.AddHours(13)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    [InlineData("123456")]              // no signature
    [InlineData("123456.")]             // empty signature
    [InlineData(".abcdef")]             // no bucket
    [InlineData("notanumber.abcdef")]
    public void Rubbish_is_refused_rather_than_throwing(string? ticket)
        => Assert.False(Service().IsValid(ticket, Noon));

    [Fact]
    public void A_ticket_cannot_be_forged_by_changing_its_bucket()
    {
        var service = Service();
        var (ticket, _) = service.Issue(Noon);
        var signature = ticket[(ticket.IndexOf('.') + 1)..];

        var futureBucket = long.Parse(ticket[..ticket.IndexOf('.')]) + 1;

        Assert.False(service.IsValid($"{futureBucket}.{signature}", Noon.AddHours(6)));
    }

    [Fact]
    public void A_ticket_from_one_deployment_is_worthless_at_another()
    {
        // The key is derived from the JWT signing secret, so rotating that invalidates every
        // outstanding media URL as well — which is the point of having a key at all.
        var (ticket, _) = Service().Issue(Noon);

        Assert.False(Service("a completely different signing secret value").IsValid(ticket, Noon));
    }
}

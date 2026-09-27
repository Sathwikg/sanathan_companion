using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Sign in with Google: a verified Google identity either opens an existing session, starts a
/// registration, or asks an existing account for its password before the two are joined.
/// </summary>
public class GoogleSignInTests
{
    private static GoogleSignInDto Google(string token = TestHarness.FakeGoogleVerifier.ValidToken) => new() { IdToken = token };

    private static GoogleRegisterDto Registration(string ticket, string mobile = "9876543210") => new()
    {
        Ticket = ticket,
        FullName = "Ravi Kumar",
        MobileNumber = mobile,
        Password = "a quiet lamp",
        ConfirmPassword = "a quiet lamp",
        SeekerName = "Ravi Seeker"
    };

    private static RegisterRequestDto PlainRegistration(string email, string mobile) => new()
    {
        FullName = "Ravi Kumar",
        Email = email,
        MobileNumber = mobile,
        Password = "a quiet lamp",
        ConfirmPassword = "a quiet lamp"
    };

    // ---------------------------------------------------------------- first sign-in

    [Fact]
    public async Task A_new_address_is_asked_to_register_and_gets_a_ticket_not_a_session()
    {
        using var harness = new TestHarness();

        var result = await harness.AuthService.SignInWithGoogleAsync(Google());

        Assert.NotNull(result);
        Assert.Equal(GoogleOutcomes.RegistrationRequired, result!.Outcome);
        Assert.Null(result.Session);
        Assert.False(string.IsNullOrWhiteSpace(result.Ticket));
        Assert.Equal("seeker@gmail.com", result.Email);
        Assert.Equal("Ravi Kumar", result.FullName);
        Assert.True(result.TicketExpiresAtUtc > DateTime.UtcNow);
        Assert.Empty(await harness.Context.Users.Where(u => u.Email == "seeker@gmail.com").ToListAsync());
    }

    [Fact]
    public async Task Registering_with_the_ticket_creates_a_linked_Sanathan_account_and_signs_it_in()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;

        var session = await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));

        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.Equal("Sanathan", session.Role);
        Assert.Equal("seeker@gmail.com", session.Email);

        var user = await harness.Context.Users.Include(u => u.Role).SingleAsync(u => u.Email == "seeker@gmail.com");
        Assert.Equal("google-sub-1001", user.GoogleSubject);
        Assert.NotNull(user.GoogleLinkedAtUtc);
        Assert.NotNull(user.EmailVerifiedAtUtc);
        Assert.Equal("9876543210", user.MobileNumber);
        Assert.Equal("Ravi Seeker", user.SeekerName);
        // The password chosen on the form is the one email + password will use from now on.
        Assert.True(harness.Hasher.Verify("a quiet lamp", user.PasswordHash));
    }

    [Fact]
    public async Task The_second_Google_sign_in_is_instant()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));

        var again = await harness.AuthService.SignInWithGoogleAsync(Google());

        Assert.Equal(GoogleOutcomes.SignedIn, again!.Outcome);
        Assert.NotNull(again.Session);
        Assert.Null(again.Ticket);
        Assert.Equal("Sanathan", again.Session!.Role);
    }

    [Fact]
    public async Task A_Google_registered_account_can_still_sign_in_with_email_and_password()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));

        var byEmail = await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "seeker@gmail.com", Password = "a quiet lamp" });
        var byMobile = await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "9876543210", Password = "a quiet lamp" });

        Assert.NotNull(byEmail);
        Assert.NotNull(byMobile);
    }

    [Fact]
    public async Task Linked_accounts_are_matched_on_the_Google_id_even_if_the_address_changed()
    {
        // The seeker renamed their Google account. The id is what identifies them, not the address.
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));

        harness.Google.Identity = harness.Google.Identity with { Email = "renamed@gmail.com" };
        var again = await harness.AuthService.SignInWithGoogleAsync(Google());

        Assert.Equal(GoogleOutcomes.SignedIn, again!.Outcome);
        Assert.Equal("seeker@gmail.com", again.Session!.Email);
    }

    // ---------------------------------------------------------------- linking an existing account

    [Fact]
    public async Task An_existing_password_account_is_asked_for_its_password_before_linking()
    {
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(PlainRegistration("seeker@gmail.com", "9123456780"));

        var result = await harness.AuthService.SignInWithGoogleAsync(Google());

        Assert.Equal(GoogleOutcomes.LinkRequired, result!.Outcome);
        Assert.Null(result.Session);
        Assert.False(string.IsNullOrWhiteSpace(result.Ticket));
        Assert.Equal("seeker@gmail.com", result.Email);

        var user = await harness.Context.Users.SingleAsync(u => u.Email == "seeker@gmail.com");
        Assert.Null(user.GoogleSubject);
    }

    [Fact]
    public async Task The_right_password_links_and_signs_in_and_later_sign_ins_are_instant()
    {
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(PlainRegistration("seeker@gmail.com", "9123456780"));
        var ask = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;

        var session = await harness.AuthService.LinkGoogleAsync(new GoogleLinkDto { Ticket = ask.Ticket!, Password = "a quiet lamp" });

        Assert.NotNull(session);
        Assert.Equal("Sanathan", session!.Role);

        var user = await harness.Context.Users.SingleAsync(u => u.Email == "seeker@gmail.com");
        Assert.Equal("google-sub-1001", user.GoogleSubject);
        Assert.NotNull(user.EmailVerifiedAtUtc);

        var again = await harness.AuthService.SignInWithGoogleAsync(Google());
        Assert.Equal(GoogleOutcomes.SignedIn, again!.Outcome);
    }

    [Fact]
    public async Task A_wrong_password_links_nothing_and_the_ticket_can_be_retried()
    {
        // This is the whole point of asking: somebody who registered your address with a password
        // you do not know must not be able to walk into the account by owning the Gmail inbox, and
        // somebody who owns the inbox must not be able to walk into an account they did not create.
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(PlainRegistration("seeker@gmail.com", "9123456780"));
        var ask = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;

        var wrong = await harness.AuthService.LinkGoogleAsync(new GoogleLinkDto { Ticket = ask.Ticket!, Password = "not the password" });

        Assert.Null(wrong);
        var user = await harness.Context.Users.SingleAsync(u => u.Email == "seeker@gmail.com");
        Assert.Null(user.GoogleSubject);

        var right = await harness.AuthService.LinkGoogleAsync(new GoogleLinkDto { Ticket = ask.Ticket!, Password = "a quiet lamp" });
        Assert.NotNull(right);
    }

    [Fact]
    public async Task A_Google_account_cannot_be_connected_to_two_seekers()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));

        // A second, unlinked account whose address Google now vouches for with the SAME subject.
        await harness.AuthService.RegisterAsync(PlainRegistration("other@gmail.com", "9123456781"));
        harness.Google.Identity = harness.Google.Identity with { Email = "other@gmail.com" };

        // The subject is already linked, so this is a sign-in to the first account, not a link.
        var result = await harness.AuthService.SignInWithGoogleAsync(Google());
        Assert.Equal(GoogleOutcomes.SignedIn, result!.Outcome);
        Assert.Equal("seeker@gmail.com", result.Session!.Email);
    }

    // ---------------------------------------------------------------- refusals

    [Fact]
    public async Task A_token_Google_does_not_vouch_for_is_refused_without_a_reason()
    {
        using var harness = new TestHarness();

        Assert.Null(await harness.AuthService.SignInWithGoogleAsync(Google("forged-or-expired")));
        Assert.Null(await harness.AuthService.SignInWithGoogleAsync(Google("")));
    }

    [Fact]
    public async Task An_unverified_Google_email_is_refused()
    {
        using var harness = new TestHarness();
        harness.Google.Identity = harness.Google.Identity with { EmailVerified = false };

        Assert.Null(await harness.AuthService.SignInWithGoogleAsync(Google()));
    }

    [Fact]
    public async Task A_closed_account_is_rejected_whether_linked_or_not()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        var session = await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));
        var admin = await harness.Context.Context_Admin();
        var users = new Sanathana.Companion.Application.Services.UserService(harness.UnitOfWork, harness.Hasher, new Sanathana.Companion.Infrastructure.Persistence.AccountDataReader(harness.Context));

        await users.SetActiveAsync(session.UserId, isActive: false, actingUserId: admin.UserId);

        var linked = await harness.AuthService.SignInWithGoogleAsync(Google());
        Assert.Equal(GoogleOutcomes.Rejected, linked!.Outcome);
        Assert.Contains("closed", linked.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- tickets

    [Fact]
    public async Task A_tampered_or_expired_ticket_is_refused()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;

        var tampered = first.Ticket![..^4] + "AAAA";
        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.RegisterWithGoogleAsync(Registration(tampered)));
        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.RegisterWithGoogleAsync(Registration("not.a.ticket")));

        // Minted eleven minutes ago: past its ten-minute life.
        var (old, _) = harness.GoogleTickets.Issue(GoogleTicketPurposes.Register, new GoogleTicket("google-sub-1001", "seeker@gmail.com"), DateTime.UtcNow.AddMinutes(-11));
        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.RegisterWithGoogleAsync(Registration(old)));
    }

    [Fact]
    public async Task A_register_ticket_cannot_link_and_a_link_ticket_cannot_register()
    {
        using var harness = new TestHarness();
        var (registerTicket, _) = harness.GoogleTickets.Issue(GoogleTicketPurposes.Register, new GoogleTicket("google-sub-1001", "seeker@gmail.com"), DateTime.UtcNow);
        var (linkTicket, _) = harness.GoogleTickets.Issue(GoogleTicketPurposes.Link, new GoogleTicket("google-sub-1001", "seeker@gmail.com"), DateTime.UtcNow);

        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.LinkGoogleAsync(new GoogleLinkDto { Ticket = registerTicket, Password = "a quiet lamp" }));
        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.RegisterWithGoogleAsync(Registration(linkTicket)));
    }

    [Fact]
    public void The_ticket_round_trips_and_reads_nothing_else()
    {
        using var harness = new TestHarness();
        var now = DateTime.UtcNow;
        var (ticket, expiresAt) = harness.GoogleTickets.Issue(GoogleTicketPurposes.Link, new GoogleTicket("sub-42", "a@b.com"), now);

        Assert.True(expiresAt > now.AddMinutes(9));
        Assert.Equal(new GoogleTicket("sub-42", "a@b.com"), harness.GoogleTickets.TryRead(GoogleTicketPurposes.Link, ticket, now));
        Assert.Null(harness.GoogleTickets.TryRead(GoogleTicketPurposes.Register, ticket, now));
        Assert.Null(harness.GoogleTickets.TryRead(GoogleTicketPurposes.Link, ticket, expiresAt));
        Assert.Null(harness.GoogleTickets.TryRead(GoogleTicketPurposes.Link, null, now));
        Assert.Null(harness.GoogleTickets.TryRead(GoogleTicketPurposes.Link, "garbage", now));
    }

    // ---------------------------------------------------------------- registration form

    [Fact]
    public async Task The_email_comes_from_the_ticket_and_the_form_must_still_pass_the_shared_rules()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;

        var mismatch = Registration(first.Ticket!);
        mismatch.ConfirmPassword = "different";
        await Assert.ThrowsAsync<ValidationException>(() => harness.AuthService.RegisterWithGoogleAsync(mismatch));

        var badMobile = Registration(first.Ticket!, mobile: "12345");
        await Assert.ThrowsAsync<ValidationException>(() => harness.AuthService.RegisterWithGoogleAsync(badMobile));

        // The local part of the address Google vouched for is inside the password.
        var identity = Registration(first.Ticket!);
        identity.Password = identity.ConfirmPassword = "seeker at the lamp";
        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.RegisterWithGoogleAsync(identity));
    }

    [Fact]
    public async Task Registering_with_a_mobile_number_somebody_else_holds_is_a_conflict()
    {
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(PlainRegistration("other@example.com", "9876543210"));
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;

        await Assert.ThrowsAsync<ConflictException>(() => harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!, mobile: "9876543210")));
    }

    [Fact]
    public async Task If_somebody_registers_the_address_between_the_two_calls_the_ticket_no_longer_registers()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        await harness.AuthService.RegisterAsync(PlainRegistration("seeker@gmail.com", "9123456780"));

        await Assert.ThrowsAsync<ConflictException>(() => harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!)));

        // ...and the next Google sign-in correctly turns into a link request instead.
        var next = await harness.AuthService.SignInWithGoogleAsync(Google());
        Assert.Equal(GoogleOutcomes.LinkRequired, next!.Outcome);
    }

    [Fact]
    public async Task The_profile_says_whether_Google_is_connected()
    {
        using var harness = new TestHarness();
        var first = (await harness.AuthService.SignInWithGoogleAsync(Google()))!;
        var session = await harness.AuthService.RegisterWithGoogleAsync(Registration(first.Ticket!));
        var users = new Sanathana.Companion.Application.Services.UserService(harness.UnitOfWork, harness.Hasher, new Sanathana.Companion.Infrastructure.Persistence.AccountDataReader(harness.Context));

        var profile = await users.GetMyProfileAsync(session.UserId);

        Assert.True(profile!.GoogleLinked);
    }
}

internal static class TestContextExtensions
{
    /// <summary>The seeded administrator, for tests that need an acting admin.</summary>
    public static async Task<Sanathana.Companion.Domain.Entities.User> Context_Admin(this Sanathana.Companion.Infrastructure.Persistence.ApplicationDbContext context)
        => await context.Users.FirstAsync(u => u.UserId == Sanathana.Companion.Infrastructure.Seed.SeedConstants.AdminUserId);
}

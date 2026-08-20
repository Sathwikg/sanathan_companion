using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Api.Configuration;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Tests;

/// <summary>
/// How long a session lasts, and how it ends: refresh-token rotation, reuse detection, and the two
/// ways an already-issued token stops working.
/// </summary>
public class SessionLifetimeTests
{
    private static RegisterRequestDto Registration(string email = "seeker@example.com", string mobile = "9876543210") => new()
    {
        FullName = "Ravi Kumar",
        Email = email,
        MobileNumber = mobile,
        Password = "a quiet lamp",
        ConfirmPassword = "a quiet lamp"
    };

    private static LoginRequestDto Login(string credential = "seeker@example.com") => new()
    {
        Credential = credential,
        Password = "a quiet lamp"
    };

    private static async Task<AuthResponseDto> SignInAsync(TestHarness harness)
    {
        await harness.AuthService.RegisterAsync(Registration());
        return (await harness.AuthService.LoginAsync(Login()))!;
    }

    private static UserService Users(TestHarness harness)
        => new(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));

    // ---------------------------------------------------------------- rotation

    [Fact]
    public async Task Signing_in_hands_back_a_refresh_token_as_well()
    {
        using var harness = new TestHarness();

        var session = await SignInAsync(harness);

        Assert.NotEmpty(session.Token);
        Assert.NotEmpty(session.RefreshToken);
        Assert.True(session.RefreshExpiresAtUtc > session.ExpiresAtUtc,
            "the refresh token has to outlive the access token or it buys nothing");
    }

    [Fact]
    public async Task Only_the_hash_of_a_refresh_token_is_stored()
    {
        // A database dump should be a list of dead hashes, not a set of live sessions.
        using var harness = new TestHarness();

        var session = await SignInAsync(harness);
        var row = await harness.Context.RefreshTokens.SingleAsync();

        Assert.NotEqual(session.RefreshToken, row.TokenHash);
        Assert.Equal(harness.RefreshTokens.Hash(session.RefreshToken), row.TokenHash);
    }

    [Fact]
    public async Task A_refresh_returns_a_new_pair_and_retires_the_old_one()
    {
        using var harness = new TestHarness();
        var first = await SignInAsync(harness);

        var second = await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = first.RefreshToken });

        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second!.RefreshToken);
        Assert.Equal("Sanathan", second.Role);

        var retired = await harness.Context.RefreshTokens
            .SingleAsync(t => t.TokenHash == harness.RefreshTokens.Hash(first.RefreshToken));
        Assert.NotNull(retired.RevokedAtUtc);
        Assert.Equal("rotated", retired.RevokedReason);
        Assert.NotNull(retired.ReplacedByTokenId);
    }

    [Fact]
    public async Task A_refreshed_token_keeps_the_role_it_had()
    {
        // The trap this pins: loading the user without their Role mints a token with an empty role
        // claim, which reads as a permissions bug rather than an authentication one.
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(Registration());

        var first = (await harness.AuthService.LoginAsync(Login()))!;
        var second = await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = first.RefreshToken });

        Assert.Equal(first.Role, second!.Role);
        Assert.NotEmpty(second.Role);
    }

    // ---------------------------------------------------------------- reuse detection

    [Fact]
    public async Task Presenting_a_token_twice_revokes_the_whole_family()
    {
        // Either it was stolen or the real device replayed it, and there is no way to tell which
        // from here. Both parties sign in again.
        using var harness = new TestHarness();
        var first = await SignInAsync(harness);

        var second = await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = first.RefreshToken });
        Assert.NotNull(second);

        var replayed = await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = first.RefreshToken });
        Assert.Null(replayed);

        // ...and the successor is dead too, which is the whole point.
        var afterwards = await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = second!.RefreshToken });
        Assert.Null(afterwards);

        Assert.All(await harness.Context.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task An_unknown_or_empty_refresh_token_is_simply_refused()
    {
        using var harness = new TestHarness();
        await SignInAsync(harness);

        Assert.Null(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = "" }));
        Assert.Null(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = "not a token" }));
    }

    [Fact]
    public async Task Signing_out_kills_the_family()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);

        await harness.AuthService.LogoutAsync(new RefreshRequestDto { RefreshToken = session.RefreshToken });

        Assert.Null(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = session.RefreshToken }));
    }

    // ---------------------------------------------------------------- changing a password

    [Fact]
    public async Task Changing_a_password_signs_out_every_other_device_but_not_this_one()
    {
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(Registration());

        var phone = (await harness.AuthService.LoginAsync(Login()))!;
        var laptop = (await harness.AuthService.LoginAsync(Login()))!;

        var renewed = await harness.AuthService.ChangePasswordAsync(phone.UserId, new ChangePasswordDto
        {
            CurrentPassword = "a quiet lamp",
            NewPassword = "a distant bell",
            ConfirmNewPassword = "a distant bell"
        });

        Assert.NotNull(renewed);
        Assert.NotEmpty(renewed!.RefreshToken);
        Assert.Equal("Sanathan", renewed.Role);

        // Both of the old sessions are gone, including the one that asked.
        Assert.Null(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = phone.RefreshToken }));
        Assert.Null(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = laptop.RefreshToken }));

        // The pair handed back in its place still works, so the seeker who took the precaution is
        // not signed out by it.
        Assert.NotNull(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = renewed.RefreshToken }));
    }

    [Fact]
    public async Task A_wrong_current_password_changes_nothing()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);

        var result = await harness.AuthService.ChangePasswordAsync(session.UserId, new ChangePasswordDto
        {
            CurrentPassword = "not the password",
            NewPassword = "a distant bell",
            ConfirmNewPassword = "a distant bell"
        });

        Assert.Null(result);
        Assert.NotNull(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = session.RefreshToken }));
    }

    // ---------------------------------------------------------------- closing an account

    [Fact]
    public async Task A_closed_account_cannot_sign_in_or_refresh()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);
        var admin = await harness.Context.Users.FirstAsync(u => u.UserId != session.UserId);

        await Users(harness).SetActiveAsync(session.UserId, isActive: false, actingUserId: admin.UserId);

        Assert.Null(await harness.AuthService.LoginAsync(Login()));
        Assert.Null(await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = session.RefreshToken }));
    }

    [Fact]
    public async Task And_its_live_access_tokens_stop_working_too()
    {
        // The reason the cut-off column exists: without it a closed account keeps working for the
        // rest of its token's life, which is up to two hours of nothing having happened.
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);
        var admin = await harness.Context.Users.FirstAsync(u => u.UserId != session.UserId);
        var validity = new TokenValidityService(harness.Context);

        var issuedAt = DateTime.UtcNow.AddMinutes(-5);
        Assert.True(await validity.IsStillValidAsync(session.UserId, issuedAt));

        await Users(harness).SetActiveAsync(session.UserId, isActive: false, actingUserId: admin.UserId);

        Assert.False(await validity.IsStillValidAsync(session.UserId, issuedAt));
    }

    [Fact]
    public async Task Reopening_an_account_lets_it_sign_in_again()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);
        var admin = await harness.Context.Users.FirstAsync(u => u.UserId != session.UserId);
        var users = Users(harness);

        await users.SetActiveAsync(session.UserId, isActive: false, actingUserId: admin.UserId);
        await users.SetActiveAsync(session.UserId, isActive: true, actingUserId: admin.UserId);

        Assert.NotNull(await harness.AuthService.LoginAsync(Login()));
    }

    [Fact]
    public async Task An_administrator_cannot_close_their_own_account_or_the_built_in_one()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);
        var admin = await harness.Context.Users.FirstAsync(u => u.UserId != session.UserId);
        var users = Users(harness);

        // Locking yourself out is a support call, not a security feature.
        await Assert.ThrowsAsync<BadRequestException>(
            () => users.SetActiveAsync(admin.UserId, false, actingUserId: admin.UserId));

        // And the seeded administrator is the only account the bootstrapper can re-open, and it
        // only ever touches the password hash.
        await Assert.ThrowsAsync<BadRequestException>(
            () => users.SetActiveAsync(admin.UserId, false, actingUserId: session.UserId));
    }

    [Fact]
    public async Task A_deleted_account_takes_its_refresh_tokens_with_it()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);

        await Users(harness).DeleteMyAccountAsync(session.UserId, "a quiet lamp");

        Assert.Empty(await harness.Context.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task And_a_deleted_account_fails_the_validity_check_rather_than_throwing()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);
        await Users(harness).DeleteMyAccountAsync(session.UserId, "a quiet lamp");

        var validity = new TokenValidityService(harness.Context);

        Assert.False(await validity.IsStillValidAsync(session.UserId, DateTime.UtcNow));
    }

    // ---------------------------------------------------------------- reading the cut-off

    [Fact]
    public async Task The_mint_time_is_read_from_the_token_the_service_actually_issues()
    {
        // This is the one that would have caught the real bug. The first revocation check cast
        // context.SecurityToken to JwtSecurityToken and fell back to "now" when the cast failed —
        // and on this stack it always failed, so every token passed the cut-off comparison and
        // nothing was ever revoked. It compiled, it looked right, and it did nothing.
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(Registration());
        var session = (await harness.AuthService.LoginAsync(Login()))!;

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(session.Token).Claims;
        var issuedAt = TokenIssuedAt.From(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        Assert.NotNull(issuedAt);
        Assert.True(Math.Abs((DateTime.UtcNow - issuedAt!.Value).TotalMinutes) < 2,
            $"nbf should be about now, was {issuedAt}");
    }

    [Fact]
    public void A_token_with_no_issue_time_yields_null_so_the_caller_fails_closed()
    {
        Assert.Null(TokenIssuedAt.From(null));
        Assert.Null(TokenIssuedAt.From(new ClaimsPrincipal(new ClaimsIdentity())));
        Assert.Null(TokenIssuedAt.From(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(JwtRegisteredClaimNames.Nbf, "not a number") }))));
    }

    [Fact]
    public async Task A_token_minted_before_the_cut_off_is_refused()
    {
        using var harness = new TestHarness();
        var session = await SignInAsync(harness);
        var admin = await harness.Context.Users.FirstAsync(u => u.UserId != session.UserId);
        var validity = new TokenValidityService(harness.Context);

        await Users(harness).SetActiveAsync(session.UserId, isActive: false, actingUserId: admin.UserId);
        await Users(harness).SetActiveAsync(session.UserId, isActive: true, actingUserId: admin.UserId);

        // Reopening does not resurrect the sessions that were killed when it closed.
        Assert.False(await validity.IsStillValidAsync(session.UserId, DateTime.UtcNow.AddMinutes(-10)));
        Assert.True(await validity.IsStillValidAsync(session.UserId, DateTime.UtcNow.AddMinutes(1)));
    }
}

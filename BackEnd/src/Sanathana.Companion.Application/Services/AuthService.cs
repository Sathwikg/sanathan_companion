using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Common;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenFactory _refreshTokens;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly IGoogleIdTokenVerifier _google;
    private readonly IGoogleTicketService _googleTickets;
    private readonly IValidator<GoogleRegisterDto> _googleRegisterValidator;
    private readonly IValidator<GoogleLinkDto> _googleLinkValidator;
    private readonly IAuditSessionTracker _sessions;

    public AuthService(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenFactory refreshTokens,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IGoogleIdTokenVerifier google,
        IGoogleTicketService googleTickets,
        IValidator<GoogleRegisterDto> googleRegisterValidator,
        IValidator<GoogleLinkDto> googleLinkValidator,
        IAuditSessionTracker sessions)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokens = refreshTokens;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _google = google;
        _googleTickets = googleTickets;
        _googleRegisterValidator = googleRegisterValidator;
        _googleLinkValidator = googleLinkValidator;
        _sessions = sessions;
    }

    public async Task<string> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Normalised before the existence checks, not after: the unique indexes are on the
        // normalised spelling, so a check against the raw string would miss the collision and
        // leave the database to raise it as a 500.
        var email = CredentialNormalizer.Email(request.Email);
        var mobile = CredentialNormalizer.Mobile(request.MobileNumber)!;   // validator guarantees ten digits

        // Deliberate: this does confirm that an account exists. Closing that oracle means
        // answering 200 and quietly not creating the account, and with no mail sender, no
        // verification and no reset endpoint, that hands anyone who forgot they had signed up a
        // dead end they cannot get out of. Revisit when forgot-password exists. The message names
        // neither the address nor which of the two credentials matched.
        if (await _uow.Users.EmailExistsAsync(email, cancellationToken)
            || await _uow.Users.MobileExistsAsync(mobile, cancellationToken))
        {
            throw new ConflictException(
                "An account already exists with these details. Please sign in, or use a different email or mobile number.");
        }

        var role = await _uow.Roles.GetByNameAsync(RoleNames.Sanathan, cancellationToken)
                   ?? throw new NotFoundException($"Default role '{RoleNames.Sanathan}' is not configured.");

        // Region is optional at sign-up; when supplied it must be a real, active region.
        if (request.RegionId is { } regionId)
        {
            var region = await _uow.Regions.GetByIdAsync(regionId, cancellationToken);
            if (region is null || !region.IsActive)
                throw new BadRequestException("Please choose a valid region.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            MobileNumber = mobile,
            PasswordHash = _passwordHasher.Hash(request.Password),
            SeekerName = string.IsNullOrWhiteSpace(request.SeekerName) ? null : request.SeekerName.Trim(),
            DefaultRegionId = request.RegionId,
            RoleId = role.RoleId
        };

        await _uow.Users.AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return "Registration Successful";
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        // The repository normalises; it is the one that knows which spelling the columns hold.
        var user = await _uow.Users.GetByEmailOrMobileAsync(request.Credential.Trim(), cancellationToken);

        // Known residual: an unknown credential short-circuits past the hash comparison, so a
        // reply arrives measurably sooner than for a real account. Equalising it means verifying
        // against a throwaway hash on the miss path, which is worth doing the day this endpoint
        // matters more than the rate limiter in front of it.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        // Checked AFTER the hash, deliberately. Short-circuiting on the flag would answer a closed
        // account in microseconds and a live one in BCrypt time, which is an enumeration oracle
        // that no amount of message-wording hides.
        if (!user.IsActive) return null;

        var response = await IssueAsync(user, familyId: Guid.NewGuid(), cancellationToken);
        await CommitSignInAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponseDto?> RefreshAsync(RefreshRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return null;

        var now = DateTime.UtcNow;
        var stored = await _uow.RefreshTokens.GetByHashAsync(_refreshTokens.Hash(request.RefreshToken), cancellationToken);
        if (stored is null) return null;

        // Already used. Either it was stolen and is being replayed, or the real device replayed it,
        // and there is no way to tell which from here, so the whole family goes and both parties
        // sign in again. Without this, rotation buys nothing.
        if (stored.RevokedAtUtc is not null)
        {
            await _uow.RefreshTokens.RevokeFamilyAsync(stored.FamilyId, "reuse-detected", now, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            _sessions.Closed(stored.FamilyId, AuditExitReasons.ReuseDetected);
            return null;
        }

        if (stored.ExpiresAtUtc <= now) return null;
        if (!stored.User.IsActive) return null;

        // The account may have had every session revoked since this token was minted.
        if (stored.CreatedDate < stored.User.TokensValidFromUtc) return null;

        stored.RevokedAtUtc = now;
        stored.RevokedReason = "rotated";

        var response = await IssueAsync(stored.User, stored.FamilyId, cancellationToken, opensSession: false);
        stored.ReplacedByTokenId = _lastIssuedId;

        await _uow.SaveChangesAsync(cancellationToken);
        _sessions.Heartbeat(stored.FamilyId);
        return response;
    }

    public async Task LogoutAsync(RefreshRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return;

        var stored = await _uow.RefreshTokens.GetByHashAsync(_refreshTokens.Hash(request.RefreshToken), cancellationToken);
        if (stored is null) return;

        await _uow.RefreshTokens.RevokeFamilyAsync(stored.FamilyId, "signed-out", DateTime.UtcNow, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        _sessions.Closed(stored.FamilyId, AuditExitReasons.ExplicitLogout);
    }

    public async Task<AuthResponseDto?> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Tracked AND role-loading, both on purpose: a plain FindAsync leaves Role null, and the
        // token reissued below reads Role.RoleName, so an administrator changing their password
        // would receive a token with no role and be locked out of their own screens.
        var user = await _uow.Users.GetTrackedWithRoleAsync(userId, cancellationToken)
                   ?? throw new NotFoundException("Your account could not be found.");

        // Re-checking the current password is what stops a stolen or borrowed session from locking
        // the real owner out of their own account.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return null;

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Changing a password is what someone does when they think another person has it. So every
        // access token minted before now dies, and every refresh token with it, and then this one
        // device gets a fresh pair: the seeker who took the precaution is not signed out by it.
        user.TokensValidFromUtc = now;
        await _uow.RefreshTokens.RevokeAllForUserAsync(userId, "password-changed", now, cancellationToken);

        var familyId = Guid.NewGuid();
        var response = await IssueAsync(user, familyId, cancellationToken);
        await CommitSignInAsync(cancellationToken);
        _sessions.ClosedForUser(userId, AuditExitReasons.PasswordChanged, exceptSessionId: familyId);

        return response;
    }

    // ------------------------------------------------------------------ Google

    public async Task<GoogleSignInResultDto?> SignInWithGoogleAsync(GoogleSignInDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken)) return null;

        var identity = await _google.VerifyAsync(request.IdToken, cancellationToken);

        // An unverified address proves nothing about who is typing, and everything below hangs
        // off the address. Google sets the flag false only for a few legacy account types, so
        // this refuses almost nobody and closes the one door that matters.
        if (identity is null || !identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
            return null;

        var now = DateTime.UtcNow;
        var email = CredentialNormalizer.Email(identity.Email);

        // Linked already: match on Google's id and nothing else. The address is not consulted,
        // because a Google account can change its address and because an address match is
        // exactly what an impostor with a pre-registered account would be hoping for.
        var linked = await _uow.Users.GetTrackedByGoogleSubjectAsync(identity.Subject, cancellationToken);
        if (linked is not null)
        {
            if (!linked.IsActive)
                return new GoogleSignInResultDto { Outcome = GoogleOutcomes.Rejected, Message = "This account has been closed." };

            var session = await IssueAsync(linked, familyId: Guid.NewGuid(), cancellationToken);
            await CommitSignInAsync(cancellationToken);
            return new GoogleSignInResultDto { Outcome = GoogleOutcomes.SignedIn, Session = session };
        }

        // Not linked. The address decides between "new seeker" and "existing account", and in
        // the second case the account's own password has to be typed once before the link is
        // made: plain registration never verifies an email, so the account holding this address
        // may have been created by somebody else who knew the address but not the inbox.
        var byEmail = await _uow.Users.GetTrackedByEmailAsync(email, cancellationToken);
        if (byEmail is not null && !byEmail.IsActive)
            return new GoogleSignInResultDto { Outcome = GoogleOutcomes.Rejected, Message = "This account has been closed." };

        var purpose = byEmail is null ? GoogleTicketPurposes.Register : GoogleTicketPurposes.Link;
        var (ticket, expiresAt) = _googleTickets.Issue(purpose, new GoogleTicket(identity.Subject, email), now);

        return new GoogleSignInResultDto
        {
            Outcome = byEmail is null ? GoogleOutcomes.RegistrationRequired : GoogleOutcomes.LinkRequired,
            Ticket = ticket,
            TicketExpiresAtUtc = expiresAt,
            Email = email,
            FullName = byEmail is null ? identity.Name?.Trim() : null
        };
    }

    public async Task<AuthResponseDto> RegisterWithGoogleAsync(GoogleRegisterDto request, CancellationToken cancellationToken = default)
    {
        await _googleRegisterValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = DateTime.UtcNow;
        var ticket = _googleTickets.TryRead(GoogleTicketPurposes.Register, request.Ticket, now)
                     ?? throw new BadRequestException("Your Google sign-in has expired. Please try again.");

        // The email is the ticket's, never the form's: the form could name any address, and the
        // whole point of this path is that Google already vouched for one.
        var email = ticket.Email;
        var mobile = CredentialNormalizer.Mobile(request.MobileNumber)!;   // validator guarantees ten digits

        if (IdentityInPassword.Contains(email, mobile, request.Password))
            throw new BadRequestException("Your password must not contain your email address or mobile number.");

        // Same oracle as RegisterAsync, for the same reason; the first check also covers the window
        // between the sign-in call and this one, during which somebody may have registered.
        if (await _uow.Users.EmailExistsAsync(email, cancellationToken))
            throw new ConflictException("An account already exists with this email address. Please sign in with Google again to connect it.");
        if (await _uow.Users.MobileExistsAsync(mobile, cancellationToken))
            throw new ConflictException("An account already exists with this mobile number. Please sign in, or use a different number.");
        if (await _uow.Users.GetTrackedByGoogleSubjectAsync(ticket.GoogleSubject, cancellationToken) is not null)
            throw new ConflictException("This Google account is already connected to another account.");

        var role = await _uow.Roles.GetByNameAsync(RoleNames.Sanathan, cancellationToken)
                   ?? throw new NotFoundException($"Default role '{RoleNames.Sanathan}' is not configured.");

        if (request.RegionId is { } regionId)
        {
            var region = await _uow.Regions.GetByIdAsync(regionId, cancellationToken);
            if (region is null || !region.IsActive)
                throw new BadRequestException("Please choose a valid region.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            MobileNumber = mobile,
            // A password is chosen once here so email + password keeps working for this account;
            // Google is a second door, not the only one.
            PasswordHash = _passwordHasher.Hash(request.Password),
            SeekerName = string.IsNullOrWhiteSpace(request.SeekerName) ? null : request.SeekerName.Trim(),
            DefaultRegionId = request.RegionId,
            RoleId = role.RoleId,
            // Role is set as well as RoleId: the token minted below reads Role.RoleName, and a
            // freshly constructed entity has no navigation loaded.
            Role = role,
            GoogleSubject = ticket.GoogleSubject,
            GoogleLinkedAtUtc = now,
            EmailVerifiedAtUtc = now
        };

        await _uow.Users.AddAsync(user, cancellationToken);

        // Signed in straight away, unlike the plain form, which sends the seeker back to the login
        // screen: they have just proved who they are to Google, and asking them to type the
        // password they chose ten seconds ago would be theatre.
        var response = await IssueAsync(user, familyId: Guid.NewGuid(), cancellationToken);
        await CommitSignInAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponseDto?> LinkGoogleAsync(GoogleLinkDto request, CancellationToken cancellationToken = default)
    {
        await _googleLinkValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = DateTime.UtcNow;
        var ticket = _googleTickets.TryRead(GoogleTicketPurposes.Link, request.Ticket, now)
                     ?? throw new BadRequestException("Your Google sign-in has expired. Please try again.");

        var user = await _uow.Users.GetTrackedByEmailAsync(ticket.Email, cancellationToken)
                   ?? throw new NotFoundException("Your account could not be found. Please sign in with Google again.");

        // The password is the account's consent to be joined to this Google account. Checked
        // before the closed-account test for the same enumeration reason LoginAsync gives.
        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        if (!user.IsActive)
            throw new BadRequestException("This account has been closed.");

        var holder = await _uow.Users.GetTrackedByGoogleSubjectAsync(ticket.GoogleSubject, cancellationToken);
        if (holder is not null && holder.UserId != user.UserId)
            throw new ConflictException("This Google account is already connected to another account.");

        user.GoogleSubject = ticket.GoogleSubject;
        user.GoogleLinkedAtUtc = now;
        // Google has just confirmed the address the account was registered with.
        user.EmailVerifiedAtUtc ??= now;

        var response = await IssueAsync(user, familyId: Guid.NewGuid(), cancellationToken);
        await CommitSignInAsync(cancellationToken);
        return response;
    }

    /// <summary>Id of the row <see cref="IssueAsync"/> created last, so rotation can link to it.</summary>
    private Guid _lastIssuedId;

    /// <summary>The sign-in <see cref="IssueAsync"/> started, held until it has been committed.</summary>
    private (Guid SessionId, Guid UserId, string? Email)? _pendingSession;

    /// <summary>
    /// Saves, and only then records the new session: a sign-in whose tokens failed to save must not
    /// appear in the session log.
    /// </summary>
    private async Task CommitSignInAsync(CancellationToken cancellationToken)
    {
        await _uow.SaveChangesAsync(cancellationToken);

        if (_pendingSession is { } pending)
        {
            _pendingSession = null;
            _sessions.Opened(pending.SessionId, pending.UserId, pending.Email);
        }
    }

    /// <summary>
    /// Mints an access token and a refresh token for a user. Does not save; the caller commits.
    /// </summary>
    /// <remarks>
    /// The user must arrive with Role loaded. The access token reads Role.RoleName, and a null
    /// role yields a token carrying no role at all, which fails in a way that looks like a
    /// permissions bug rather than an authentication one.
    /// </remarks>
    private async Task<AuthResponseDto> IssueAsync(User user, Guid familyId, CancellationToken cancellationToken, bool opensSession = true)
    {
        // The family id doubles as the session id: it is stable across every refresh of this
        // sign-in, so the access token can name its session and page visits can be tied to it.
        var (token, expiresAt) = _jwtTokenService.GenerateToken(user, familyId);
        if (opensSession) _pendingSession = (familyId, user.UserId, user.Email);
        var (refresh, hash) = _refreshTokens.Create();
        var refreshExpiresAt = _refreshTokens.ExpiresAt(DateTime.UtcNow);

        var row = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = hash,
            FamilyId = familyId,
            ExpiresAtUtc = refreshExpiresAt
        };

        await _uow.RefreshTokens.AddAsync(row, cancellationToken);
        _lastIssuedId = row.Id;

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            RefreshToken = refresh,
            RefreshExpiresAtUtc = refreshExpiresAt,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            SeekerName = user.SeekerName,
            Role = user.Role?.RoleName ?? string.Empty
        };
    }
}

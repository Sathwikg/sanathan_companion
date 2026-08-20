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

    public AuthService(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenFactory refreshTokens,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ChangePasswordDto> changePasswordValidator)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokens = refreshTokens;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
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
        await _uow.SaveChangesAsync(cancellationToken);
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
            return null;
        }

        if (stored.ExpiresAtUtc <= now) return null;
        if (!stored.User.IsActive) return null;

        // The account may have had every session revoked since this token was minted.
        if (stored.CreatedDate < stored.User.TokensValidFromUtc) return null;

        stored.RevokedAtUtc = now;
        stored.RevokedReason = "rotated";

        var response = await IssueAsync(stored.User, stored.FamilyId, cancellationToken);
        stored.ReplacedByTokenId = _lastIssuedId;

        await _uow.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(RefreshRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return;

        var stored = await _uow.RefreshTokens.GetByHashAsync(_refreshTokens.Hash(request.RefreshToken), cancellationToken);
        if (stored is null) return;

        await _uow.RefreshTokens.RevokeFamilyAsync(stored.FamilyId, "signed-out", DateTime.UtcNow, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
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

        var response = await IssueAsync(user, familyId: Guid.NewGuid(), cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return response;
    }

    /// <summary>Id of the row <see cref="IssueAsync"/> created last, so rotation can link to it.</summary>
    private Guid _lastIssuedId;

    /// <summary>
    /// Mints an access token and a refresh token for a user. Does not save; the caller commits.
    /// </summary>
    /// <remarks>
    /// The user must arrive with Role loaded. The access token reads Role.RoleName, and a null
    /// role yields a token carrying no role at all, which fails in a way that looks like a
    /// permissions bug rather than an authentication one.
    /// </remarks>
    private async Task<AuthResponseDto> IssueAsync(User user, Guid familyId, CancellationToken cancellationToken)
    {
        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
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

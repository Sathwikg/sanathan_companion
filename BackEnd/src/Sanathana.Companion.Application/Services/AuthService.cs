using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;

    public AuthService(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ChangePasswordDto> changePasswordValidator)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    public async Task<string> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = request.Email.Trim();
        if (await _uow.Users.EmailExistsAsync(email, cancellationToken))
            throw new ConflictException($"A user with email '{email}' already exists.");

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
            MobileNumber = request.MobileNumber.Trim(),
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

        var user = await _uow.Users.GetByEmailOrMobileAsync(request.Credential.Trim(), cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            SeekerName = user.SeekerName,
            Role = user.Role?.RoleName ?? string.Empty
        };
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException("Your account could not be found.");

        // Re-checking the current password is what stops a stolen or borrowed session from locking
        // the real owner out of their own account.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);

        return true;
    }
}

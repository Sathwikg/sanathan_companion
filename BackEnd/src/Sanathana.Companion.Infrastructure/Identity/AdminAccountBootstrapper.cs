using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Interfaces;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <summary>
/// Opens the seeded administrator account once, from configuration, if it is still locked.
/// </summary>
/// <remarks>
/// The seeded account ships with a hash nothing verifies against, so a fresh deployment has no way
/// in until an operator supplies a password out of band — the same posture as
/// <c>JwtSettings:Secret</c>. Supply it as the <c>Admin__InitialPassword</c> environment variable.
/// <para>
/// It applies ONLY while the stored hash is still the sentinel. Once the password has been set —
/// here or through <c>POST /api/auth/change-password</c> — this never touches the account again, so
/// leaving the variable set in the environment cannot silently reset a rotated password, and
/// removing it cannot lock anyone out.
/// </para>
/// </remarks>
public class AdminAccountBootstrapper
{
    public const string ConfigurationKey = "Admin:InitialPassword";

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAccountBootstrapper> _logger;

    public AdminAccountBootstrapper(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IConfiguration configuration,
        ILogger<AdminAccountBootstrapper> logger)
    {
        _uow = uow;
        _hasher = hasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var admin = await _uow.Users.GetByIdAsync(SeedConstants.AdminUserId, cancellationToken);
        if (admin is null) return;

        // Already opened, or already rotated. Never touch it again.
        if (!string.Equals(admin.PasswordHash, SeedConstants.AdminPasswordHash, StringComparison.Ordinal))
            return;

        var password = _configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            // Not fatal: the rest of the app serves seekers perfectly well without an administrator
            // signed in, and refusing to boot would take the whole site down over it.
            _logger.LogWarning(
                "The administrator account is locked and {Key} is not set, so nobody can sign in as admin. " +
                "Set the Admin__InitialPassword environment variable and restart to open it.",
                ConfigurationKey);
            return;
        }

        if (!PasswordPolicy.IsAcceptable(password))
        {
            _logger.LogWarning(
                "{Key} was ignored because it does not meet the password policy (at least {Minimum} characters, " +
                "and not an obvious one). The administrator account remains locked.",
                ConfigurationKey, PasswordPolicy.MinimumLength);
            return;
        }

        admin.PasswordHash = _hasher.Hash(password);
        _uow.Users.Update(admin);
        await _uow.SaveChangesAsync(cancellationToken);

        // Deliberately not logging the password, the hash, or anything derived from them.
        _logger.LogInformation("The administrator account password was set from {Key}.", ConfigurationKey);
    }
}

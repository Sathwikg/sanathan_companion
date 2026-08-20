using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <inheritdoc />
public class TokenValidityService : ITokenValidityService
{
    private readonly ApplicationDbContext _context;

    public TokenValidityService(ApplicationDbContext context) => _context = context;

    public async Task<bool> IsStillValidAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default)
    {
        var row = await _context.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.IsActive, u.TokensValidFromUtc })
            .FirstOrDefaultAsync(cancellationToken);

        // A deleted account is not a valid one. The row going missing is the whole point of
        // DELETE profile/me, and until now the token outlived it.
        if (row is null) return false;
        if (!row.IsActive) return false;

        // One second of slack: nbf is stamped to whole seconds, so a token minted in the same
        // second as the cut-off would otherwise kill the very session that set it.
        return issuedAtUtc.AddSeconds(1) >= row.TokensValidFromUtc;
    }
}

using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => await Set.Include(t => t.User).ThenInclude(u => u.Role)
                    .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeFamilyAsync(Guid familyId, string reason, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var live = await Set.Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
                            .ToListAsync(cancellationToken);
        Revoke(live, reason, nowUtc);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var live = await Set.Where(t => t.UserId == userId && t.RevokedAtUtc == null)
                            .ToListAsync(cancellationToken);
        Revoke(live, reason, nowUtc);
    }

    // Tracked entities; the unit of work commits.
    private static void Revoke(List<RefreshToken> tokens, string reason, DateTime nowUtc)
    {
        foreach (var token in tokens)
        {
            token.RevokedAtUtc = nowUtc;
            token.RevokedReason = reason;
        }
    }
}

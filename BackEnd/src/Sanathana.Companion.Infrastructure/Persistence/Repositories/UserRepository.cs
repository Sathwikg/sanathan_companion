using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<User?> GetByEmailOrMobileAsync(string credential, CancellationToken cancellationToken = default)
        => await Set.Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == credential || u.MobileNumber == credential, cancellationToken);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllWithRolesAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Include(u => u.Role)
            .OrderByDescending(u => u.CreatedDate).ToListAsync(cancellationToken);

    public async Task<User?> GetWithRoleAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
}

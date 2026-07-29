using System.Linq.Expressions;
using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Interfaces;

/// <summary>Generic repository abstraction. Implementations must NOT call SaveChanges — persistence is owned by <see cref="IUnitOfWork"/>.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}

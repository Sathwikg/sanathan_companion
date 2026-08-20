using Sanathana.Companion.Application.DTOs.Users;

namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Assembles the "everything we hold about you" export.
/// </summary>
/// <remarks>
/// An Application-owned interface implemented in Infrastructure, like <c>IPasswordHasher</c>: the
/// export spans six aggregates and reads across them in one pass, which is a data-access concern,
/// while the shape it produces belongs to the Application layer. <c>IUserRepository</c> could not
/// host it — that lives in Domain, which must not know about DTOs.
/// </remarks>
public interface IAccountDataReader
{
    Task<MyDataExportDto?> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}

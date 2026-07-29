namespace Sanathana.Companion.Application.Interfaces;

/// <summary>Exposes the identity of the caller for the current request (implemented in the API layer).</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}

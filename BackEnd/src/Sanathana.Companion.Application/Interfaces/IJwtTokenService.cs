using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Generates a signed JWT for the user (whose Role navigation must be loaded).</summary>
    /// <param name="sessionId">The refresh-token family this token belongs to, carried as the "sid" claim.</param>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user, Guid? sessionId = null);
}

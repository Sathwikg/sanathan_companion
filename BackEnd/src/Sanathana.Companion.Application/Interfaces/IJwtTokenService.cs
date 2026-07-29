using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Generates a signed JWT for the user (whose Role navigation must be loaded).</summary>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}

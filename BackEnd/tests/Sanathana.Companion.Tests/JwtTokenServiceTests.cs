using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Identity;

namespace Sanathana.Companion.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_emits_expected_claims()
    {
        var service = new JwtTokenService(Options.Create(new JwtSettings
        {
            Secret = "sanathana-companion-test-secret-key-0123456789ABCDEF",
            Issuer = "iss",
            Audience = "aud",
            ExpiryMinutes = 30
        }));

        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Test Seeker",
            Email = "t@example.com",
            SeekerName = "Sadhaka",
            RoleId = 2,
            Role = new Role { RoleId = 2, RoleName = "Sanathan" }
        };

        var (token, expiresAt) = service.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTime.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.UserId.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("t@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("Sanathan", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal("Sadhaka", jwt.Claims.First(c => c.Type == "seekerName").Value);
        Assert.Equal("Test Seeker", jwt.Claims.First(c => c.Type == "fullName").Value);
        Assert.Equal("iss", jwt.Issuer);
    }
}

using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Validators;

namespace Sanathana.Companion.Tests;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequestDto Valid() => new()
    {
        FullName = "Ravi Kumar",
        Email = "ravi@example.com",
        MobileNumber = "9876543210",
        Password = "secret1",
        ConfirmPassword = "secret1",
        SeekerName = "Ravi Seeker"
    };

    [Fact]
    public void Accepts_valid_request()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Rejects_password_mismatch()
    {
        var dto = Valid();
        dto.ConfirmPassword = "different";
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void Rejects_invalid_email()
    {
        var dto = Valid();
        dto.Email = "not-an-email";
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void Rejects_short_password()
    {
        var dto = Valid();
        dto.Password = "123";
        dto.ConfirmPassword = "123";
        Assert.False(_validator.Validate(dto).IsValid);
    }
}

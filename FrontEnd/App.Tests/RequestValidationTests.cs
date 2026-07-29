using System.ComponentModel.DataAnnotations;
using App.Core.Models;

namespace App.Tests;

public class RequestValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static RegisterRequest ValidRegistration() => new()
    {
        FullName = "Ravi Kumar",
        Email = "ravi@example.com",
        MobileNumber = "9876543210",
        Password = "secret1",
        ConfirmPassword = "secret1",
        SeekerName = "Ravi Seeker"
    };

    [Fact]
    public void Valid_registration_passes()
        => Assert.Empty(Validate(ValidRegistration()));

    [Fact]
    public void Password_mismatch_fails()
    {
        var model = ValidRegistration();
        model.ConfirmPassword = "different";
        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var model = ValidRegistration();
        model.Email = "not-an-email";
        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void Short_password_fails()
    {
        var model = ValidRegistration();
        model.Password = "123";
        model.ConfirmPassword = "123";
        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void Empty_login_fails()
        => Assert.NotEmpty(Validate(new LoginRequest()));

    [Fact]
    public void Valid_login_passes()
        => Assert.Empty(Validate(new LoginRequest { Credential = "admin", Password = "admin" }));
}

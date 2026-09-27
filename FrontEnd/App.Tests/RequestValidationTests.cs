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
        Password = "a quiet lamp",
        ConfirmPassword = "a quiet lamp",
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

    private static GoogleRegisterRequest ValidGoogleRegistration() => new()
    {
        Ticket = "ticket-from-the-api",
        FullName = "Ravi Kumar",
        MobileNumber = "9876543210",
        Password = "a quiet lamp",
        ConfirmPassword = "a quiet lamp"
    };

    [Fact]
    public void Valid_google_registration_passes()
        => Assert.Empty(Validate(ValidGoogleRegistration()));

    [Fact]
    public void Google_registration_still_needs_a_mobile_number_and_a_real_password()
    {
        var noMobile = ValidGoogleRegistration();
        noMobile.MobileNumber = string.Empty;
        Assert.NotEmpty(Validate(noMobile));

        var shortPassword = ValidGoogleRegistration();
        shortPassword.Password = shortPassword.ConfirmPassword = "short";
        Assert.NotEmpty(Validate(shortPassword));
    }

    [Fact]
    public void Google_registration_has_no_email_field()
    {
        // The address is the one Google verified, carried in the ticket. A field here would be
        // an invitation to type a different one, which the server would ignore anyway.
        Assert.Null(typeof(GoogleRegisterRequest).GetProperty("Email"));
    }

    [Fact]
    public void Empty_login_fails()
        => Assert.NotEmpty(Validate(new LoginRequest()));

    [Fact]
    public void Valid_login_passes()
        => Assert.Empty(Validate(new LoginRequest { Credential = "admin", Password = "admin" }));
}

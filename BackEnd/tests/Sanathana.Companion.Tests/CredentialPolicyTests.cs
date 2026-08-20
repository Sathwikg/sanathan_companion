using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Common;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The password rule, the credential normaliser, and the promise that registration answers the
/// same thing whichever credential was already taken.
/// </summary>
/// <remarks>
/// The uniqueness itself is proved through the ConflictException path only: TestHarness runs on
/// the EF InMemory provider, which ignores unique indexes entirely. The index is exercised by the
/// NormalizeUserCredentials migration against a real PostgreSQL.
/// </remarks>
public class CredentialPolicyTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly ChangePasswordValidator _change = new();

    private static RegisterRequestDto Registration(string password) => new()
    {
        FullName = "Ravi Kumar",
        Email = "ravi@example.com",
        MobileNumber = "9876543210",
        Password = password,
        ConfirmPassword = password
    };

    private static ChangePasswordDto Change(string? newPassword) => new()
    {
        CurrentPassword = "the old passphrase",
        NewPassword = newPassword!,
        ConfirmNewPassword = newPassword!
    };

    // ---------------------------------------------------------------- the policy itself

    [Theory]
    [InlineData("nine char", false)]          // nine characters
    [InlineData("ten charac", true)]          // exactly the minimum
    [InlineData("aaaaaaaaaa", false)]         // one character repeated
    [InlineData("password123", false)]        // on the obvious list
    [InlineData("PASSWORD123", false)]        // the list is case-insensitive
    [InlineData("a quiet lamp", true)]        // letters and spaces only, and that is fine
    public void The_policy_decides_on_length_not_on_character_classes(string password, bool accepted)
        => Assert.Equal(accepted, PasswordPolicy.IsAcceptable(password));

    [Fact]
    public void A_null_password_is_rejected_rather_than_throwing()
        => Assert.False(PasswordPolicy.IsAcceptable(null));

    [Fact]
    public void The_length_ceiling_is_measured_in_bytes_because_that_is_what_BCrypt_truncates()
    {
        // Thirty Devanagari characters sit well under any character-count ceiling and well over
        // seventy-two bytes, which is exactly the passphrase BCrypt would silently cut short.
        var devanagari = string.Concat(Enumerable.Repeat("क", 30));

        Assert.Equal(30, devanagari.Length);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(devanagari) > PasswordPolicy.MaximumByteLength);
        Assert.False(PasswordPolicy.IsAcceptable(devanagari));
        Assert.False(_register.Validate(Registration(devanagari)).IsValid);
        Assert.False(_change.Validate(Change(devanagari)).IsValid);
    }

    [Fact]
    public void Registration_and_change_password_now_answer_to_the_same_rule()
    {
        Assert.False(_register.Validate(Registration("nine char")).IsValid);
        Assert.False(_change.Validate(Change("nine char")).IsValid);

        Assert.True(_register.Validate(Registration("ten charac")).IsValid);
        Assert.True(_change.Validate(Change("ten charac")).IsValid);
    }

    [Fact]
    public void A_null_new_password_fails_validation_instead_of_throwing()
        => Assert.False(_change.Validate(Change(null)).IsValid);

    // ---------------------------------------------------------------- the identity rule

    [Fact]
    public void A_password_built_from_the_email_or_the_number_is_refused()
    {
        Assert.False(_register.Validate(Registration("ravi and more")).IsValid);
        Assert.False(_register.Validate(Registration("my 9876543210")).IsValid);
    }

    [Fact]
    public void But_a_one_letter_local_part_does_not_ban_every_password_containing_that_letter()
    {
        // Without the length floor, an address of m@example.com would reject anything with an m.
        var dto = Registration("a quiet lamp");
        dto.Email = "m@example.com";

        Assert.True(_register.Validate(dto).IsValid);
    }

    // ---------------------------------------------------------------- normalisation

    [Theory]
    [InlineData("  Foo@Example.COM ", "foo@example.com")]
    [InlineData("already@lower.com", "already@lower.com")]
    public void Email_normalises_to_one_spelling(string typed, string stored)
        => Assert.Equal(stored, CredentialNormalizer.Email(typed));

    [Theory]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("098765 43210", "9876543210")]
    [InlineData("9876543210", "9876543210")]
    [InlineData("0000000000", "0000000000")]     // the seeded administrator
    [InlineData("+++++++", null)]
    public void Mobile_normalises_to_the_digits_that_matter(string typed, string? stored)
        => Assert.Equal(stored, CredentialNormalizer.Mobile(typed));

    [Fact]
    public void A_number_typed_with_punctuation_still_reaches_the_same_row()
        => Assert.Equal(CredentialNormalizer.Mobile("9876543210"), CredentialNormalizer.Mobile("+91-98765-43210"));

    [Fact]
    public void A_mobile_number_with_fewer_than_ten_digits_is_refused_by_the_validator()
    {
        var dto = Registration("a quiet lamp");
        dto.MobileNumber = "+91-999";   // passes the character regex, five digits once normalised

        var result = _register.Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequestDto.MobileNumber));
    }

    // ---------------------------------------------------------------- registration behaviour

    [Fact]
    public async Task Two_spellings_of_one_address_are_one_account()
    {
        using var harness = new TestHarness();

        await harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            FullName = "Ravi",
            Email = "Ravi@Example.com",
            MobileNumber = "9876543210",
            Password = "a quiet lamp",
            ConfirmPassword = "a quiet lamp"
        });

        var stored = await harness.UnitOfWork.Users.GetByEmailOrMobileAsync("RAVI@EXAMPLE.COM");
        Assert.NotNull(stored);
        Assert.Equal("ravi@example.com", stored!.Email);

        await Assert.ThrowsAsync<ConflictException>(() => harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            FullName = "Someone else",
            Email = "ravi@example.com",
            MobileNumber = "9111111110",
            Password = "another passphrase",
            ConfirmPassword = "another passphrase"
        }));
    }

    [Fact]
    public async Task A_number_already_registered_under_another_spelling_is_a_conflict_too()
    {
        using var harness = new TestHarness();

        await harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            FullName = "Ravi",
            Email = "ravi@example.com",
            MobileNumber = "+91 98765 43210",
            Password = "a quiet lamp",
            ConfirmPassword = "a quiet lamp"
        });

        await Assert.ThrowsAsync<ConflictException>(() => harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            FullName = "Someone else",
            Email = "other@example.com",
            // Not "another passphrase": it contains "other", which the identity rule refuses.
            Password = "a distant bell",
            MobileNumber = "09876543210",
            ConfirmPassword = "a distant bell"
        }));
    }

    [Fact]
    public async Task The_conflict_message_does_not_say_which_credential_was_taken()
    {
        using var harness = new TestHarness();

        await harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            FullName = "Ravi",
            Email = "taken@example.com",
            MobileNumber = "9876543210",
            Password = "a quiet lamp",
            ConfirmPassword = "a quiet lamp"
        });

        var byEmail = await Assert.ThrowsAsync<ConflictException>(() => harness.AuthService.RegisterAsync(
            new RegisterRequestDto
            {
                FullName = "B",
                Email = "taken@example.com",
                MobileNumber = "9111111110",
                Password = "another passphrase",
                ConfirmPassword = "another passphrase"
            }));

        var byMobile = await Assert.ThrowsAsync<ConflictException>(() => harness.AuthService.RegisterAsync(
            new RegisterRequestDto
            {
                FullName = "C",
                Email = "free@example.com",
                MobileNumber = "9876543210",
                Password = "another passphrase",
                ConfirmPassword = "another passphrase"
            }));

        Assert.DoesNotContain("taken@example.com", byEmail.Message);
        Assert.DoesNotContain("9876543210", byMobile.Message);
        Assert.Equal(byEmail.Message, byMobile.Message);
    }
}

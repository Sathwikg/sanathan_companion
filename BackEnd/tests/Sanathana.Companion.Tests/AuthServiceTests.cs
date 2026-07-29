using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

public class AuthServiceTests
{
    private static RegisterRequestDto NewRegistration(string email = "ravi@example.com", string mobile = "9876543210") => new()
    {
        FullName = "Ravi Kumar",
        Email = email,
        MobileNumber = mobile,
        Password = "secret1",
        ConfirmPassword = "secret1",
        SeekerName = "Ravi Seeker"
    };

    [Fact]
    public async Task Register_assigns_Sanathan_role_hashes_password_and_returns_success()
    {
        using var harness = new TestHarness();

        var message = await harness.AuthService.RegisterAsync(NewRegistration());

        Assert.Equal("Registration Successful", message);

        var user = await harness.Context.Users.Include(u => u.Role)
            .FirstAsync(u => u.Email == "ravi@example.com");
        Assert.Equal("Sanathan", user.Role.RoleName);
        Assert.Equal(2, user.RoleId);
        Assert.NotEqual("secret1", user.PasswordHash);
        Assert.True(harness.Hasher.Verify("secret1", user.PasswordHash));
        Assert.Equal("Ravi Seeker", user.SeekerName);
    }

    [Fact]
    public async Task Register_duplicate_email_throws_Conflict()
    {
        using var harness = new TestHarness();

        await harness.AuthService.RegisterAsync(NewRegistration("dup@example.com"));

        await Assert.ThrowsAsync<ConflictException>(
            () => harness.AuthService.RegisterAsync(NewRegistration("dup@example.com", "9111111110")));
    }

    [Fact]
    public async Task Register_password_mismatch_throws_Validation()
    {
        using var harness = new TestHarness();

        var dto = NewRegistration();
        dto.ConfirmPassword = "different";

        await Assert.ThrowsAsync<ValidationException>(() => harness.AuthService.RegisterAsync(dto));
    }

    [Fact]
    public async Task Login_admin_admin_succeeds_with_Admin_role()
    {
        using var harness = new TestHarness();

        var result = await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "admin", Password = "admin" });

        Assert.NotNull(result);
        Assert.Equal("Admin", result!.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_wrong_password_returns_null()
    {
        using var harness = new TestHarness();

        var result = await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "admin", Password = "wrong" });

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_by_mobile_number_succeeds()
    {
        using var harness = new TestHarness();
        await harness.AuthService.RegisterAsync(NewRegistration("m@example.com", "9123456780"));

        var result = await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "9123456780", Password = "secret1" });

        Assert.NotNull(result);
        Assert.Equal("m@example.com", result!.Email);
        Assert.Equal("Sanathan", result.Role);
    }

    [Fact]
    public async Task Register_stores_the_chosen_region_as_the_default()
    {
        using var harness = new TestHarness();
        var region = new Sanathana.Companion.Domain.Entities.Region
        {
            Id = Guid.NewGuid(), Name = "SignupRegion", IsActive = true
        };
        harness.Context.Regions.Add(region);
        await harness.Context.SaveChangesAsync();

        var dto = NewRegistration("region@example.com", "9123400000");
        dto.RegionId = region.Id;

        await harness.AuthService.RegisterAsync(dto);

        var user = await harness.Context.Users.FirstAsync(u => u.Email == "region@example.com");
        Assert.Equal(region.Id, user.DefaultRegionId);
    }

    [Fact]
    public async Task Register_without_a_region_is_allowed()
    {
        using var harness = new TestHarness();

        await harness.AuthService.RegisterAsync(NewRegistration("noregion@example.com", "9123400001"));

        var user = await harness.Context.Users.FirstAsync(u => u.Email == "noregion@example.com");
        Assert.Null(user.DefaultRegionId);
    }

    [Fact]
    public async Task Register_with_an_unknown_region_throws_BadRequest()
    {
        using var harness = new TestHarness();

        var dto = NewRegistration("badregion@example.com", "9123400002");
        dto.RegionId = Guid.NewGuid();

        await Assert.ThrowsAsync<BadRequestException>(() => harness.AuthService.RegisterAsync(dto));
    }
}

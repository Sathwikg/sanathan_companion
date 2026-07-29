using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class RegisterRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^[0-9+\-\s]{7,15}$", ErrorMessage = "Enter a valid mobile number.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(72, MinimumLength = 6, ErrorMessage = "Password must be 6–72 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(150)]
    public string? SeekerName { get; set; }

    /// <summary>Optional preferred region — becomes the seeker's default region.</summary>
    public Guid? RegionId { get; set; }
}

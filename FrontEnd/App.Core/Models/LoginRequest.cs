using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Email or mobile number is required.")]
    public string Credential { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

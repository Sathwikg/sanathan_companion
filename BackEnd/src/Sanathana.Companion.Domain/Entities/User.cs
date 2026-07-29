using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Application user / seeker.</summary>
public class User : BaseEntity
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Optional spiritual name used for personalized greetings.</summary>
    public string? SeekerName { get; set; }

    /// <summary>The user's preferred region — seeds the app's region selector on sign-in.</summary>
    public Guid? DefaultRegionId { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

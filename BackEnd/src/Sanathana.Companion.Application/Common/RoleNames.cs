namespace Sanathana.Companion.Application.Common;

/// <summary>Well-known role names seeded into the Roles table.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Sanathan = "Sanathan";

    /// <summary>
    /// The visitor who chose "Continue as Guest". No account ever holds this role: a guest sends
    /// no token at all, and the module filter evaluates such a caller against this row's grants on
    /// the endpoints marked [GuestAccess]. It exists as a real row so an administrator decides on
    /// the Access Rights screen what a guest may browse, exactly as for any other role.
    /// </summary>
    public const string Guest = "Guest";
}

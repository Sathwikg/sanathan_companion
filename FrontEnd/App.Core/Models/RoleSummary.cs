namespace App.Core.Models;

/// <summary>
/// What the "Your Role" popup needs: the signed-in user's role as displayed, what it means, and
/// the flag the UI styles by (never the name, which may be translated).
/// </summary>
public sealed record RoleSummary(string Name, string? Description, bool IsAdmin);

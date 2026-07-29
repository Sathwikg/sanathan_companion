namespace Sanathana.Companion.Application.Common;

/// <summary>The kinds of item a user can favorite.</summary>
public static class FavoriteTypes
{
    public const string Chant = "Chant";
    public const string Deity = "Deity";

    public static bool IsValid(string? type)
        => string.Equals(type, Chant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, Deity, StringComparison.OrdinalIgnoreCase);

    /// <summary>Normalises user input to the canonical casing.</summary>
    public static string Normalize(string type)
        => string.Equals(type, Chant, StringComparison.OrdinalIgnoreCase) ? Chant : Deity;
}

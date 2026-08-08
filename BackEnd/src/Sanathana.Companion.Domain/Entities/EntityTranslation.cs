using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A translated value for one field of one database row — this is what turns DB-driven content
/// (menu names, deity names, festival names…) into the selected language.
/// </summary>
/// <remarks>
/// <see cref="EntityKey"/> is a string rather than a typed FK on purpose: primary keys across the
/// domain are mixed (most masters use <c>Guid Id</c>, Role uses <c>int RoleId</c>, Day uses
/// <c>int DayId</c>), so one generic table can only address them all by their stringified key.
/// That also means there is no database-level FK to the target row — deletions are cleaned up by
/// <c>LocalizationService</c> rather than by a cascade.
/// </remarks>
public class EntityTranslation : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Entity name as used in code, e.g. "MenuModule", "Deity", "Festival".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Stringified primary key of the target row.</summary>
    public string EntityKey { get; set; } = string.Empty;

    /// <summary>Property being translated, e.g. "Name" or "Description".</summary>
    public string Field { get; set; } = string.Empty;

    public Guid LanguageId { get; set; }
    public Language? Language { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Composite lookup key used by the client bundle: "EntityType:EntityKey:Field".</summary>
    public static string BundleKey(string entityType, string entityKey, string field)
        => $"{entityType}:{entityKey}:{field}";
}

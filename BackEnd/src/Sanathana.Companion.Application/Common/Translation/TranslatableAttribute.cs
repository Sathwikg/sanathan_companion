namespace Sanathana.Companion.Application.Common.Translation;

/// <summary>
/// Marks a DTO string property whose value should be translated on the way out of the API.
/// </summary>
/// <remarks>
/// <para>
/// Translation is <b>opt-in</b>. Nothing is translated unless it carries this attribute, which is
/// what keeps personal data (names, e-mail, feedback text) out of the pipeline by construction.
/// </para>
/// <para>
/// NEVER put this on a DTO that an editor posts straight back. Several edit screens use a display
/// NAME as its own identifier (see <c>DeityEdit.razor</c>, where the region/festival/day pickers do
/// <c>new SelectOption(r, r)</c> and then post the selected names back). Translating such a property
/// makes a save write the translated text into the database and orphan the row. Annotate read/list
/// DTOs only, and let the client send <c>X-Translate: none</c> when it is loading a form for editing.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TranslatableAttribute : Attribute
{
    /// <summary>Dictionary-only translation — for controlled vocabulary that repeats across rows.</summary>
    public TranslatableAttribute() { }

    /// <summary>
    /// Per-row translation first (via <c>EntityTranslation</c>), dictionary as the fallback.
    /// </summary>
    /// <param name="entityType">Entity name as stored, e.g. "Deity".</param>
    /// <param name="keyProperty">Sibling property on the same DTO holding that row's primary key.</param>
    public TranslatableAttribute(string entityType, string keyProperty)
    {
        EntityType = entityType;
        KeyProperty = keyProperty;
    }

    public string? EntityType { get; }

    public string? KeyProperty { get; }

    /// <summary>The <c>EntityTranslation.Field</c> to look up. Defaults to the property's own name.</summary>
    public string? Field { get; set; }

    /// <summary>
    /// Run the full phrase-substitution pass rather than only a whole-value lookup. Set this for
    /// values that embed other text, such as "Navami upto 16:37, Dasami from 16:38".
    /// </summary>
    public bool Composite { get; set; }

    /// <summary>Restrict dictionary matching to one term category (e.g. "panchangam").</summary>
    public string? Category { get; set; }
}

/// <summary>
/// Hard stop — this type is never walked by the translator, whatever its properties say.
/// Applied to DTO families that carry user-entered personal data.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public sealed class NoTranslateAttribute : Attribute;

using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// The chant body written in one specific language, for one chant configuration.
/// One row per (ChantConfig, Language) — the language-wise text captured on the Chant Config form.
/// </summary>
public class ChantLanguageConfig : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChantConfigId { get; set; }
    public ChantConfig? ChantConfig { get; set; }

    public Guid LanguageId { get; set; }
    public Language? Language { get; set; }

    /// <summary>The chant text in this language, as sanitized HTML.</summary>
    public string Text { get; set; } = string.Empty;
}

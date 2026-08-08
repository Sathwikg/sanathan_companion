using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Whether one form (menu module) is allowed to render in one language.
/// A form that is not enabled stays in English even while that language is selected, which lets
/// an admin roll a language out screen by screen instead of all at once.
/// </summary>
/// <remarks>
/// Absence of a row means ENABLED: selecting a language translates the whole app by default, and
/// a row is only written when an admin deliberately opts a form out. English is never stored here
/// — it is the base language and every form always renders in it.
/// </remarks>
public class LanguageFormConfig : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LanguageId { get; set; }
    public Language? Language { get; set; }

    public Guid MenuModuleId { get; set; }
    public MenuModule? MenuModule { get; set; }

    public bool Enabled { get; set; }
}

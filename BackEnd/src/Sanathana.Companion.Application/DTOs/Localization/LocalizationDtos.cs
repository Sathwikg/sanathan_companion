namespace Sanathana.Companion.Application.DTOs.Localization;

/// <summary>A language the user can switch the app into.</summary>
public class LocaleDto
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    /// <summary>True for English — the base language, always complete and never editable.</summary>
    public bool IsBase { get; set; }
    /// <summary>How many labels are filled in for this language.</summary>
    public int TranslatedCount { get; set; }
    public int TotalKeys { get; set; }
}

/// <summary>
/// Everything the client needs to render one language: the merged label set, the translations for
/// DB-driven content, and which forms are allowed to show in this language.
/// </summary>
public class LocalizationBundleDto
{
    public string Code { get; set; } = string.Empty;
    public Guid LanguageId { get; set; }
    public bool IsBase { get; set; }

    /// <summary>Label key -> text, already merged with the English fallback.</summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>"EntityType:EntityKey:Field" -> translated text, for DB-driven content.</summary>
    public Dictionary<string, string> Entities { get; set; } = new();

    /// <summary>
    /// Route paths of the forms that must stay in English even while this language is selected.
    /// Translating everything is the default, so this list is normally empty — an admin opts a
    /// specific form out on the Language Configs screen.
    /// </summary>
    public List<string> DisabledRoutes { get; set; } = new();

    /// <summary>Bumped whenever a translation changes so clients can cache-bust.</summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>One row of the label editor: English on the left, the target language on the right.</summary>
public class LabelEditRowDto
{
    public string Key { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

public class LabelEditorDto
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public List<string> Namespaces { get; set; } = new();
    public List<LabelEditRowDto> Rows { get; set; } = new();
}

public class SaveLabelItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SaveLabelsDto
{
    public List<SaveLabelItemDto> Items { get; set; } = new();
}

/// <summary>One form and whether it may render in the selected language.</summary>
public class LanguageFormDto
{
    public Guid ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? RoutePath { get; set; }
    public string? ParentName { get; set; }
    public bool IsParent { get; set; }
    public bool Enabled { get; set; }
}

public class LanguageFormMatrixDto
{
    public Guid LanguageId { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public List<LanguageFormDto> Forms { get; set; } = new();
}

public class SaveLanguageFormItemDto
{
    public Guid ModuleId { get; set; }
    public bool Enabled { get; set; }
}

public class SaveLanguageFormsDto
{
    public List<SaveLanguageFormItemDto> Items { get; set; } = new();
}

/// <summary>One translatable piece of DB content, with its English original.</summary>
public class EntityTranslationRowDto
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

public class SaveEntityTranslationsDto
{
    public List<EntityTranslationRowDto> Items { get; set; } = new();
}

// ----------------------------------------------------------------------------
// All-languages-at-once editing. The single-language DTOs above still back the
// per-language views; these carry every language in one grid so a label can be
// written in Telugu, Hindi, Tamil and Kannada without switching screens.
// ----------------------------------------------------------------------------

/// <summary>A column in the translation grid.</summary>
public class MatrixLanguageDto
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    /// <summary>English — shown read-only as the source column.</summary>
    public bool IsBase { get; set; }
}

/// <summary>A selectable scope: one form, or a shared section such as "common".</summary>
public class TranslationScopeDto
{
    /// <summary>The key namespace, e.g. "deities" or "common".</summary>
    public string Namespace { get; set; } = string.Empty;
    /// <summary>Human label — the form's menu name, or the namespace for shared sections.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Set when this scope is an actual form.</summary>
    public Guid? ModuleId { get; set; }
    public string? RoutePath { get; set; }
    public string? Icon { get; set; }
    /// <summary>True for shared vocabulary (common/nav/msg/auth) rather than a single form.</summary>
    public bool IsShared { get; set; }
    public int KeyCount { get; set; }
}

/// <summary>One label across every language.</summary>
public class MatrixRowDto
{
    public string Key { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
    /// <summary>LanguageId -> text. Missing or blank means "falls back to English".</summary>
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class TranslationMatrixDto
{
    public List<MatrixLanguageDto> Languages { get; set; } = new();
    public List<TranslationScopeDto> Scopes { get; set; } = new();
    public string? Scope { get; set; }
    public List<MatrixRowDto> Rows { get; set; } = new();
}

public class SaveMatrixRowDto
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class SaveMatrixDto
{
    public List<SaveMatrixRowDto> Rows { get; set; } = new();
}

/// <summary>DB content (menu names, deity names…) across every language.</summary>
public class EntityMatrixRowDto
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class EntityMatrixDto
{
    public List<MatrixLanguageDto> Languages { get; set; } = new();
    public List<EntityMatrixRowDto> Rows { get; set; } = new();
}

public class SaveEntityMatrixDto
{
    public List<EntityMatrixRowDto> Rows { get; set; } = new();
}

// ---- Dictionary (shared term) admin surface ----

public class HarvestSourceResultDto
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int DistinctValues { get; set; }
    public int NewTerms { get; set; }
}

public class HarvestResultDto
{
    public int Added { get; set; }
    public int FromRuntimeMisses { get; set; }
    /// <summary>Shipped translations applied to the newly discovered terms.</summary>
    public int SeededTranslations { get; set; }
    public List<HarvestSourceResultDto> BySource { get; set; } = new();
}

/// <summary>One dictionary term across every language.</summary>
public class DictionaryRowDto
{
    public Guid TermId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    /// <summary>How often the app hit this value and could not translate it.</summary>
    public int MissCount { get; set; }
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class DictionaryPageDto
{
    public List<MatrixLanguageDto> Languages { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<DictionaryRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    /// <summary>Terms with no text in at least one language — the size of the remaining work.</summary>
    public int MissingCount { get; set; }
}

public class SaveDictionaryRowDto
{
    public Guid TermId { get; set; }
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class SaveDictionaryDto
{
    public List<SaveDictionaryRowDto> Rows { get; set; } = new();
}

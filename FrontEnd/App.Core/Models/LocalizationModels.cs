namespace App.Core.Models;

/// <summary>A language the user can switch the app into.</summary>
public class LocaleModel
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsBase { get; set; }
    public int TranslatedCount { get; set; }
    public int TotalKeys { get; set; }

    /// <summary>Native name when we have one, otherwise the English name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(NativeName) ? Name : NativeName!;
}

/// <summary>Everything needed to render the app in one language.</summary>
public class LocalizationBundle
{
    public string Code { get; set; } = "en";
    public Guid LanguageId { get; set; }
    public bool IsBase { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Entities { get; set; } = new();
    public List<string> DisabledRoutes { get; set; } = new();
    public string Version { get; set; } = string.Empty;
}

public class LabelEditRow
{
    public string Key { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

public class LabelEditorModel
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public List<string> Namespaces { get; set; } = new();
    public List<LabelEditRow> Rows { get; set; } = new();
}

public class SaveLabelItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SaveLabelsRequest
{
    public List<SaveLabelItem> Items { get; set; } = new();
}

public class LanguageFormModel
{
    public Guid ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? RoutePath { get; set; }
    public string? ParentName { get; set; }
    public bool IsParent { get; set; }
    public bool Enabled { get; set; }
}

public class LanguageFormMatrixModel
{
    public Guid LanguageId { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public List<LanguageFormModel> Forms { get; set; } = new();
}

public class SaveLanguageFormItem
{
    public Guid ModuleId { get; set; }
    public bool Enabled { get; set; }
}

public class SaveLanguageFormsRequest
{
    public List<SaveLanguageFormItem> Items { get; set; } = new();
}

public class EntityTranslationRow
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

public class SaveEntityTranslationsRequest
{
    public List<EntityTranslationRow> Items { get; set; } = new();
}

// ---- All languages in one grid ----

public class MatrixLanguage
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsBase { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(NativeName) ? Name : NativeName!;
}

public class TranslationScope
{
    public string Namespace { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid? ModuleId { get; set; }
    public string? RoutePath { get; set; }
    public string? Icon { get; set; }
    public bool IsShared { get; set; }
    public int KeyCount { get; set; }
}

public class MatrixRow
{
    public string Key { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
    public Dictionary<Guid, string> Values { get; set; } = new();

    /// <summary>Two-way binding helper — the grid writes straight into <see cref="Values"/>.</summary>
    public string Get(Guid languageId) => Values.TryGetValue(languageId, out var v) ? v : string.Empty;
    public void Set(Guid languageId, string value) => Values[languageId] = value ?? string.Empty;
}

public class TranslationMatrix
{
    public List<MatrixLanguage> Languages { get; set; } = new();
    public List<TranslationScope> Scopes { get; set; } = new();
    public string? Scope { get; set; }
    public List<MatrixRow> Rows { get; set; } = new();
}

public class SaveMatrixRow
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class SaveMatrixRequest
{
    public List<SaveMatrixRow> Rows { get; set; } = new();
}

public class EntityMatrixRow
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public Dictionary<Guid, string> Values { get; set; } = new();

    public string Get(Guid languageId) => Values.TryGetValue(languageId, out var v) ? v : string.Empty;
    public void Set(Guid languageId, string value) => Values[languageId] = value ?? string.Empty;
}

public class EntityMatrix
{
    public List<MatrixLanguage> Languages { get; set; } = new();
    public List<EntityMatrixRow> Rows { get; set; } = new();
}

public class SaveEntityMatrixRequest
{
    public List<EntityMatrixRow> Rows { get; set; } = new();
}

// ---- Shared term dictionary (translates database text) ----

public class DictionaryRow
{
    public Guid TermId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public int MissCount { get; set; }
    public Dictionary<Guid, string> Values { get; set; } = new();

    public string Get(Guid languageId) => Values.TryGetValue(languageId, out var v) ? v : string.Empty;
}

public class DictionaryPage
{
    public List<MatrixLanguage> Languages { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<DictionaryRow> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int MissingCount { get; set; }
}

public class SaveDictionaryRow
{
    public Guid TermId { get; set; }
    public Dictionary<Guid, string> Values { get; set; } = new();
}

public class SaveDictionaryRequest
{
    public List<SaveDictionaryRow> Rows { get; set; } = new();
}

public class HarvestSourceResult
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int DistinctValues { get; set; }
    public int NewTerms { get; set; }
}

public class HarvestResult
{
    public int Added { get; set; }
    public int FromRuntimeMisses { get; set; }
    public int SeededTranslations { get; set; }
    public List<HarvestSourceResult> BySource { get; set; } = new();
}

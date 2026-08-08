using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class LocalizationService : ILocalizationService
{
    /// <summary>English is the base language: it is never edited here and always renders everywhere.</summary>
    public const string BaseCode = "en";

    private readonly IUnitOfWork _uow;
    private readonly ILocalizationSeedSource _seed;
    private readonly ITranslationCatalog? _catalog;

    public LocalizationService(IUnitOfWork uow, ILocalizationSeedSource seed, ITranslationCatalog? catalog = null)
    {
        _uow = uow;
        _seed = seed;
        _catalog = catalog;
    }

    /// <summary>
    /// Drops the cached matchers so the next request rebuilds them. Every save path must call this,
    /// or an admin's edit would not show up until the process restarted.
    /// </summary>
    private void InvalidateCatalog() => _catalog?.Invalidate();

    // ---------------------------------------------------------------- locales

    public async Task<IReadOnlyList<LocaleDto>> GetLocalesAsync(CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken))
            .Where(l => l.IsActive)
            .ToList();

        var baseLang = FindBase(languages);
        var baseKeys = baseLang is null
            ? new Dictionary<string, string>()
            : (await _uow.Localization.GetResourcesAsync(baseLang.Id, cancellationToken))
                .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        var result = new List<LocaleDto>();
        foreach (var l in languages)
        {
            var isBase = IsBase(l);
            var translated = isBase
                ? baseKeys.Count
                : (await _uow.Localization.GetResourcesAsync(l.Id, cancellationToken))
                    .Count(r => !string.IsNullOrWhiteSpace(r.Value));

            // Only offer languages that can actually render something.
            if (!isBase && translated == 0) continue;

            result.Add(new LocaleDto
            {
                LanguageId = l.Id,
                Code = (l.Code ?? string.Empty).ToLowerInvariant(),
                Name = l.Name,
                NativeName = l.NativeName,
                IsBase = isBase,
                TranslatedCount = translated,
                TotalKeys = baseKeys.Count
            });
        }

        // Base language first, then alphabetically.
        return result.OrderByDescending(x => x.IsBase).ThenBy(x => x.Name).ToList();
    }

    // ---------------------------------------------------------------- bundle

    public async Task<LocalizationBundleDto> GetBundleAsync(string code, CancellationToken cancellationToken = default)
    {
        code = (code ?? BaseCode).Trim().ToLowerInvariant();

        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var baseLang = FindBase(languages);

        var baseLabels = baseLang is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _uow.Localization.GetResourcesAsync(baseLang.Id, cancellationToken))
                .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        var target = languages.FirstOrDefault(l =>
            string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase) && l.IsActive);

        // Unknown or inactive code, or English itself → the base bundle.
        if (target is null || IsBase(target))
        {
            return new LocalizationBundleDto
            {
                Code = BaseCode,
                LanguageId = baseLang?.Id ?? Guid.Empty,
                IsBase = true,
                Labels = baseLabels,
                Version = VersionOf(baseLabels.Count, 0)
            };
        }

        // Target language overlays the English base, so a missing key silently falls back.
        var labels = new Dictionary<string, string>(baseLabels, StringComparer.Ordinal);
        foreach (var r in await _uow.Localization.GetResourcesAsync(target.Id, cancellationToken))
            if (!string.IsNullOrWhiteSpace(r.Value))
                labels[r.Key] = r.Value;

        var entities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in await _uow.Localization.GetEntityTranslationsAsync(target.Id, cancellationToken))
            if (!string.IsNullOrWhiteSpace(t.Text))
                entities[EntityTranslation.BundleKey(t.EntityType, t.EntityKey, t.Field)] = t.Text;

        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        // Only forms explicitly opted out are listed; everything else translates by default.
        var disabledModuleIds = (await _uow.Localization.GetFormConfigsAsync(target.Id, cancellationToken))
            .Where(c => !c.Enabled)
            .Select(c => c.MenuModuleId)
            .ToHashSet();

        var disabledRoutes = modules
            .Where(m => disabledModuleIds.Contains(m.Id) && !string.IsNullOrWhiteSpace(m.RoutePath))
            .Select(m => NormaliseRoute(m.RoutePath!))
            .Distinct()
            .ToList();

        return new LocalizationBundleDto
        {
            Code = code,
            LanguageId = target.Id,
            IsBase = false,
            Labels = labels,
            Entities = entities,
            DisabledRoutes = disabledRoutes,
            Version = VersionOf(labels.Count, entities.Count)
        };
    }

    // ---------------------------------------------------------------- label editor

    public async Task<LabelEditorDto> GetLabelEditorAsync(Guid languageId, CancellationToken cancellationToken = default)
    {
        var target = await RequireEditableLanguageAsync(languageId, cancellationToken);
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var baseLang = FindBase(languages)
            ?? throw new BadRequestException("The English base language is missing; run the localization import first.");

        var baseRows = await _uow.Localization.GetResourcesAsync(baseLang.Id, cancellationToken);
        var targetByKey = (await _uow.Localization.GetResourcesAsync(target.Id, cancellationToken))
            .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        var rows = baseRows
            .OrderBy(r => r.Namespace, StringComparer.Ordinal)
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new LabelEditRowDto
            {
                Key = r.Key,
                Namespace = r.Namespace,
                English = r.Value,
                Translation = targetByKey.TryGetValue(r.Key, out var v) ? v : string.Empty
            })
            .ToList();

        return new LabelEditorDto
        {
            LanguageId = target.Id,
            Code = (target.Code ?? string.Empty).ToLowerInvariant(),
            LanguageName = target.Name,
            Namespaces = rows.Select(r => r.Namespace).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList(),
            Rows = rows
        };
    }

    public async Task SaveLabelsAsync(Guid languageId, SaveLabelsDto dto, CancellationToken cancellationToken = default)
    {
        var target = await RequireEditableLanguageAsync(languageId, cancellationToken);

        var existing = (await _uow.Localization.GetResourcesTrackedAsync(target.Id, cancellationToken))
            .ToDictionary(r => r.Key, StringComparer.Ordinal);

        foreach (var item in dto.Items)
        {
            var key = (item.Key ?? string.Empty).Trim();
            if (key.Length == 0) continue;

            var value = (item.Value ?? string.Empty).Trim();

            if (existing.TryGetValue(key, out var row))
            {
                // Clearing a translation removes the row so the English fallback takes over again.
                if (value.Length == 0)
                {
                    _uow.Localization.RemoveResource(row);
                }
                else if (!string.Equals(row.Value, value, StringComparison.Ordinal))
                {
                    row.Value = value;
                    row.IsSeeded = false; // hand-edited: protect it from the next seed import
                    _uow.Localization.UpdateResource(row);
                }
            }
            else if (value.Length > 0)
            {
                await _uow.Localization.AddResourceAsync(new LocalizationResource
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Namespace = NamespaceOf(key),
                    LanguageId = target.Id,
                    Value = value,
                    IsSeeded = false
                }, cancellationToken);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    
        InvalidateCatalog();
    }

    // ---------------------------------------------------------------- form matrix

    public async Task<LanguageFormMatrixDto> GetFormMatrixAsync(Guid languageId, CancellationToken cancellationToken = default)
    {
        var target = await RequireEditableLanguageAsync(languageId, cancellationToken);

        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var byId = modules.ToDictionary(m => m.Id);
        var parentIds = modules.Where(m => m.ParentId.HasValue).Select(m => m.ParentId!.Value).ToHashSet();
        // Default is translated; a stored row with Enabled=false is an explicit opt-out.
        var disabled = (await _uow.Localization.GetFormConfigsAsync(target.Id, cancellationToken))
            .Where(c => !c.Enabled)
            .Select(c => c.MenuModuleId)
            .ToHashSet();

        var dto = new LanguageFormMatrixDto { LanguageId = target.Id, LanguageName = target.Name };
        foreach (var m in OrderForDisplay(modules))
        {
            dto.Forms.Add(new LanguageFormDto
            {
                ModuleId = m.Id,
                ModuleName = m.Name,
                Icon = m.Icon,
                RoutePath = m.RoutePath,
                ParentName = m.ParentId.HasValue && byId.TryGetValue(m.ParentId.Value, out var p) ? p.Name : null,
                IsParent = parentIds.Contains(m.Id),
                Enabled = !disabled.Contains(m.Id)
            });
        }
        return dto;
    }

    public async Task SaveFormMatrixAsync(Guid languageId, SaveLanguageFormsDto dto, CancellationToken cancellationToken = default)
    {
        var target = await RequireEditableLanguageAsync(languageId, cancellationToken);

        var validIds = (await _uow.MenuModules.GetAllOrderedAsync(cancellationToken)).Select(m => m.Id).ToHashSet();
        var existing = (await _uow.Localization.GetFormConfigsTrackedAsync(target.Id, cancellationToken))
            .ToDictionary(c => c.MenuModuleId);

        foreach (var item in dto.Items)
        {
            if (!validIds.Contains(item.ModuleId))
                throw new BadRequestException($"Form '{item.ModuleId}' does not exist.");

            // Enabled is the default, so an enabled form needs no row — storing only the
            // opt-outs keeps the table small and makes new forms translate automatically.
            if (existing.TryGetValue(item.ModuleId, out var row))
            {
                if (item.Enabled) _uow.Localization.RemoveFormConfig(row);
                else if (row.Enabled) { row.Enabled = false; _uow.Localization.UpdateFormConfig(row); }
            }
            else if (!item.Enabled)
            {
                await _uow.Localization.AddFormConfigAsync(new LanguageFormConfig
                {
                    Id = Guid.NewGuid(),
                    LanguageId = target.Id,
                    MenuModuleId = item.ModuleId,
                    Enabled = false
                }, cancellationToken);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    
        InvalidateCatalog();
    }

    // ---------------------------------------------------------------- DB content

    public async Task<IReadOnlyList<EntityTranslationRowDto>> GetEntityRowsAsync(Guid languageId, CancellationToken cancellationToken = default)
    {
        var target = await RequireEditableLanguageAsync(languageId, cancellationToken);

        var existing = (await _uow.Localization.GetEntityTranslationsAsync(target.Id, cancellationToken))
            .ToDictionary(t => EntityTranslation.BundleKey(t.EntityType, t.EntityKey, t.Field), t => t.Text, StringComparer.Ordinal);

        var rows = new List<EntityTranslationRowDto>();

        void Add(string type, string key, string field, string original)
        {
            if (string.IsNullOrWhiteSpace(original)) return;
            var bundleKey = EntityTranslation.BundleKey(type, key, field);
            rows.Add(new EntityTranslationRowDto
            {
                EntityType = type,
                EntityKey = key,
                Field = field,
                Original = original,
                Translation = existing.TryGetValue(bundleKey, out var t) ? t : string.Empty
            });
        }

        foreach (var m in await _uow.MenuModules.GetAllOrderedAsync(cancellationToken))
            Add(nameof(MenuModule), m.Id.ToString(), nameof(MenuModule.Name), m.Name);

        foreach (var d in await _uow.Deities.ListWithoutImageAsync(cancellationToken))
            Add(nameof(Deity), d.Id.ToString(), nameof(Deity.Name), d.Name);

        foreach (var c in await _uow.Chants.GetAllOrderedAsync(cancellationToken))
            Add(nameof(Chant), c.Id.ToString(), nameof(Chant.Name), c.Name);

        foreach (var r in await _uow.Regions.GetAllOrderedAsync(cancellationToken))
            Add(nameof(Region), r.Id.ToString(), nameof(Region.Name), r.Name);

        return rows;
    }

    public async Task SaveEntityRowsAsync(Guid languageId, SaveEntityTranslationsDto dto, CancellationToken cancellationToken = default)
    {
        var target = await RequireEditableLanguageAsync(languageId, cancellationToken);

        var existing = (await _uow.Localization.GetEntityTranslationsTrackedAsync(target.Id, cancellationToken))
            .ToDictionary(t => EntityTranslation.BundleKey(t.EntityType, t.EntityKey, t.Field), StringComparer.Ordinal);

        foreach (var item in dto.Items)
        {
            var type = (item.EntityType ?? string.Empty).Trim();
            var key = (item.EntityKey ?? string.Empty).Trim();
            var field = (item.Field ?? string.Empty).Trim();
            if (type.Length == 0 || key.Length == 0 || field.Length == 0) continue;

            var text = (item.Translation ?? string.Empty).Trim();
            var bundleKey = EntityTranslation.BundleKey(type, key, field);

            if (existing.TryGetValue(bundleKey, out var row))
            {
                if (text.Length == 0) _uow.Localization.RemoveEntityTranslation(row);
                else if (!string.Equals(row.Text, text, StringComparison.Ordinal))
                {
                    row.Text = text;
                    _uow.Localization.UpdateEntityTranslation(row);
                }
            }
            else if (text.Length > 0)
            {
                await _uow.Localization.AddEntityTranslationAsync(new EntityTranslation
                {
                    Id = Guid.NewGuid(),
                    EntityType = type,
                    EntityKey = key,
                    Field = field,
                    LanguageId = target.Id,
                    Text = text
                }, cancellationToken);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    
        InvalidateCatalog();
    }

    // ---------------------------------------------------------------- all-languages matrix

    /// <summary>
    /// Namespaces that are shared vocabulary rather than one form. Everything else is expected to
    /// match a form's route, which is what lets the editor be scoped form by form.
    /// </summary>
    private static readonly HashSet<string> SharedNamespaces =
        new(StringComparer.OrdinalIgnoreCase) { "common", "nav", "msg", "auth", "langcfg" };

    public async Task<TranslationMatrixDto> GetMatrixAsync(string? scope, CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken))
            .Where(l => l.IsActive)
            .OrderByDescending(IsBase)     // English first — it is the source column
            .ThenBy(l => l.Name)
            .ToList();

        var dto = new TranslationMatrixDto
        {
            Scope = scope,
            Languages = languages.Select(l => new MatrixLanguageDto
            {
                LanguageId = l.Id,
                Code = (l.Code ?? string.Empty).ToLowerInvariant(),
                Name = l.Name,
                NativeName = l.NativeName,
                IsBase = IsBase(l)
            }).ToList()
        };

        var baseLang = FindBase(languages);
        if (baseLang is null) return dto;

        var baseRows = await _uow.Localization.GetResourcesAsync(baseLang.Id, cancellationToken);
        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);

        // Every namespace becomes a scope; a form supplies its menu name and icon.
        var moduleByNamespace = modules
            .Where(m => !string.IsNullOrWhiteSpace(m.RoutePath))
            .GroupBy(m => NamespaceForRoute(m.RoutePath!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        dto.Scopes = baseRows
            .GroupBy(r => r.Namespace, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                moduleByNamespace.TryGetValue(g.Key, out var module);
                var shared = SharedNamespaces.Contains(g.Key) || module is null;
                return new TranslationScopeDto
                {
                    Namespace = g.Key,
                    Title = module?.Name ?? g.Key,
                    ModuleId = module?.Id,
                    RoutePath = module?.RoutePath,
                    Icon = module?.Icon,
                    IsShared = shared,
                    KeyCount = g.Count()
                };
            })
            .OrderByDescending(s => s.IsShared)
            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(scope)) return dto;   // scope list only

        var keys = baseRows
            .Where(r => string.Equals(r.Namespace, scope, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToList();
        if (keys.Count == 0) return dto;

        // One read per language, then pivot — avoids a query per cell.
        var valuesByLanguage = new Dictionary<Guid, Dictionary<string, string>>();
        foreach (var l in languages.Where(l => !IsBase(l)))
            valuesByLanguage[l.Id] = (await _uow.Localization.GetResourcesAsync(l.Id, cancellationToken))
                .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        foreach (var row in keys)
        {
            var m = new MatrixRowDto { Key = row.Key, Namespace = row.Namespace, English = row.Value };
            foreach (var (languageId, map) in valuesByLanguage)
                m.Values[languageId] = map.TryGetValue(row.Key, out var v) ? v : string.Empty;
            dto.Rows.Add(m);
        }

        return dto;
    }

    public async Task SaveMatrixAsync(SaveMatrixDto dto, CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken)).ToDictionary(l => l.Id);

        // Group the grid by language so each language's rows are loaded and written once.
        var byLanguage = new Dictionary<Guid, List<(string Key, string Value)>>();
        foreach (var row in dto.Rows)
        {
            var key = (row.Key ?? string.Empty).Trim();
            if (key.Length == 0) continue;

            foreach (var (languageId, value) in row.Values)
            {
                if (!languages.TryGetValue(languageId, out var language))
                    throw new BadRequestException($"Language '{languageId}' was not found.");
                if (IsBase(language))
                    continue; // English is the source text and is never written from the grid

                if (!byLanguage.TryGetValue(languageId, out var list))
                    byLanguage[languageId] = list = new List<(string, string)>();
                list.Add((key, value ?? string.Empty));
            }
        }

        foreach (var (languageId, items) in byLanguage)
        {
            var existing = (await _uow.Localization.GetResourcesTrackedAsync(languageId, cancellationToken))
                .ToDictionary(r => r.Key, StringComparer.Ordinal);

            foreach (var (key, raw) in items)
            {
                var value = raw.Trim();

                if (existing.TryGetValue(key, out var row))
                {
                    if (value.Length == 0)
                    {
                        _uow.Localization.RemoveResource(row);      // back to the English fallback
                        existing.Remove(key);
                    }
                    else if (!string.Equals(row.Value, value, StringComparison.Ordinal))
                    {
                        row.Value = value;
                        row.IsSeeded = false;                        // protect the edit from re-import
                        _uow.Localization.UpdateResource(row);
                    }
                }
                else if (value.Length > 0)
                {
                    var added = new LocalizationResource
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        Namespace = NamespaceOf(key),
                        LanguageId = languageId,
                        Value = value,
                        IsSeeded = false
                    };
                    await _uow.Localization.AddResourceAsync(added, cancellationToken);
                    existing[key] = added;                           // guard against duplicate keys in one payload
                }
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    
        InvalidateCatalog();
    }

    public async Task<EntityMatrixDto> GetEntityMatrixAsync(CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken))
            .Where(l => l.IsActive)
            .OrderByDescending(IsBase)
            .ThenBy(l => l.Name)
            .ToList();

        var dto = new EntityMatrixDto
        {
            Languages = languages.Select(l => new MatrixLanguageDto
            {
                LanguageId = l.Id,
                Code = (l.Code ?? string.Empty).ToLowerInvariant(),
                Name = l.Name,
                NativeName = l.NativeName,
                IsBase = IsBase(l)
            }).ToList()
        };

        var translations = new Dictionary<Guid, Dictionary<string, string>>();
        foreach (var l in languages.Where(l => !IsBase(l)))
            translations[l.Id] = (await _uow.Localization.GetEntityTranslationsAsync(l.Id, cancellationToken))
                .ToDictionary(t => EntityTranslation.BundleKey(t.EntityType, t.EntityKey, t.Field), t => t.Text, StringComparer.Ordinal);

        void Add(string type, string key, string field, string original)
        {
            if (string.IsNullOrWhiteSpace(original)) return;
            var bundleKey = EntityTranslation.BundleKey(type, key, field);
            var row = new EntityMatrixRowDto { EntityType = type, EntityKey = key, Field = field, Original = original };
            foreach (var (languageId, map) in translations)
                row.Values[languageId] = map.TryGetValue(bundleKey, out var v) ? v : string.Empty;
            dto.Rows.Add(row);
        }

        foreach (var m in await _uow.MenuModules.GetAllOrderedAsync(cancellationToken))
            Add(nameof(MenuModule), m.Id.ToString(), nameof(MenuModule.Name), m.Name);
        foreach (var d in await _uow.Deities.ListWithoutImageAsync(cancellationToken))
            Add(nameof(Deity), d.Id.ToString(), nameof(Deity.Name), d.Name);
        foreach (var c in await _uow.Chants.GetAllOrderedAsync(cancellationToken))
            Add(nameof(Chant), c.Id.ToString(), nameof(Chant.Name), c.Name);
        foreach (var r in await _uow.Regions.GetAllOrderedAsync(cancellationToken))
            Add(nameof(Region), r.Id.ToString(), nameof(Region.Name), r.Name);

        return dto;
    }

    public async Task SaveEntityMatrixAsync(SaveEntityMatrixDto dto, CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken)).ToDictionary(l => l.Id);

        var byLanguage = new Dictionary<Guid, List<EntityTranslationRowDto>>();
        foreach (var row in dto.Rows)
        {
            var type = (row.EntityType ?? string.Empty).Trim();
            var key = (row.EntityKey ?? string.Empty).Trim();
            var field = (row.Field ?? string.Empty).Trim();
            if (type.Length == 0 || key.Length == 0 || field.Length == 0) continue;

            foreach (var (languageId, value) in row.Values)
            {
                if (!languages.TryGetValue(languageId, out var language))
                    throw new BadRequestException($"Language '{languageId}' was not found.");
                if (IsBase(language)) continue;

                if (!byLanguage.TryGetValue(languageId, out var list))
                    byLanguage[languageId] = list = new List<EntityTranslationRowDto>();

                list.Add(new EntityTranslationRowDto
                {
                    EntityType = type,
                    EntityKey = key,
                    Field = field,
                    Translation = value ?? string.Empty
                });
            }
        }

        foreach (var (languageId, items) in byLanguage)
            await SaveEntityRowsAsync(languageId, new SaveEntityTranslationsDto { Items = items }, cancellationToken);
    
        InvalidateCatalog();
    }

    /// <summary>"/chants-config" -&gt; "chantsConfig", so a key namespace can identify its form.</summary>
    public static string NamespaceForRoute(string route)
    {
        var trimmed = (route ?? string.Empty).Trim().Trim('/');
        if (trimmed.Length == 0) return string.Empty;

        // Only the first segment identifies the form; "/chants-config/{id}/edit" is still the same form.
        var first = trimmed.Split('/')[0];
        var parts = first.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return string.Empty;

        var sb = new System.Text.StringBuilder(parts[0].ToLowerInvariant());
        for (var i = 1; i < parts.Length; i++)
            sb.Append(char.ToUpperInvariant(parts[i][0])).Append(parts[i][1..].ToLowerInvariant());
        return sb.ToString();
    }

    // ---------------------------------------------------------------- seed import / export

    public async Task<int> ImportSeedFilesAsync(CancellationToken cancellationToken = default)
    {
        var seed = _seed.Load();
        if (seed.Count == 0) return 0;

        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var written = 0;

        foreach (var (code, entries) in seed)
        {
            var language = languages.FirstOrDefault(l =>
                string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
            if (language is null) continue; // no such language row — nothing to attach the text to

            var existing = (await _uow.Localization.GetResourcesTrackedAsync(language.Id, cancellationToken))
                .ToDictionary(r => r.Key, StringComparer.Ordinal);

            foreach (var (key, value) in entries)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;

                if (existing.TryGetValue(key, out var row))
                {
                    // Never overwrite something an admin edited by hand.
                    if (!row.IsSeeded || string.Equals(row.Value, value, StringComparison.Ordinal)) continue;
                    row.Value = value;
                    row.Namespace = NamespaceOf(key);
                    _uow.Localization.UpdateResource(row);
                    written++;
                }
                else
                {
                    await _uow.Localization.AddResourceAsync(new LocalizationResource
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        Namespace = NamespaceOf(key),
                        LanguageId = language.Id,
                        Value = value,
                        IsSeeded = true
                    }, cancellationToken);
                    written++;
                }
            }
        }

        if (written > 0) await _uow.SaveChangesAsync(cancellationToken);

        written += await PruneOrphanedResourcesAsync(seed, cancellationToken);
        written += await SeedMenuEntityTranslationsAsync(cancellationToken);
        InvalidateCatalog();
        return written;
    }

    /// <summary>
    /// Drops rows whose key no longer exists in any seed file — otherwise renaming or removing a
    /// key leaves it behind forever, showing up as a phantom section in the editor.
    /// Only seed-owned rows are removed; a hand-edited row is left for an admin to clear.
    /// </summary>
    private async Task<int> PruneOrphanedResourcesAsync(
        IReadOnlyDictionary<string, Dictionary<string, string>> seed, CancellationToken cancellationToken)
    {
        // Guard: if the embedded files failed to load we must not interpret that as "delete everything".
        var known = seed.Values.SelectMany(d => d.Keys).ToHashSet(StringComparer.Ordinal);
        if (known.Count == 0) return 0;

        var removed = 0;
        foreach (var language in await _uow.Languages.GetAllOrderedAsync(cancellationToken))
        {
            foreach (var row in await _uow.Localization.GetResourcesTrackedAsync(language.Id, cancellationToken))
            {
                if (known.Contains(row.Key) || !row.IsSeeded) continue;
                _uow.Localization.RemoveResource(row);
                removed++;
            }
        }

        if (removed > 0) await _uow.SaveChangesAsync(cancellationToken);
        return removed;
    }

    /// <summary>
    /// Gives the DB-driven navigation its translations for free: every menu module whose English
    /// name matches a translated <c>nav.*</c> label gets an <see cref="EntityTranslation"/> in each
    /// language. Without this the sidebar would stay English even though the labels are translated,
    /// because the menu text comes from the database rather than the resource files.
    /// </summary>
    private async Task<int> SeedMenuEntityTranslationsAsync(CancellationToken cancellationToken)
    {
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var baseLang = FindBase(languages);
        if (baseLang is null) return 0;

        // English nav labels, indexed by their text so a module name can be looked up directly.
        var keyByEnglishText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in await _uow.Localization.GetResourcesAsync(baseLang.Id, cancellationToken))
            if (string.Equals(r.Namespace, "nav", StringComparison.Ordinal))
                keyByEnglishText[r.Value.Trim()] = r.Key;

        if (keyByEnglishText.Count == 0) return 0;

        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var written = 0;

        foreach (var language in languages.Where(l => !IsBase(l) && l.IsActive))
        {
            var labels = (await _uow.Localization.GetResourcesAsync(language.Id, cancellationToken))
                .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

            var existing = (await _uow.Localization.GetEntityTranslationsTrackedAsync(language.Id, cancellationToken))
                .Select(t => EntityTranslation.BundleKey(t.EntityType, t.EntityKey, t.Field))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var m in modules)
            {
                if (!keyByEnglishText.TryGetValue(m.Name.Trim(), out var navKey)) continue;
                if (!labels.TryGetValue(navKey, out var translated) || string.IsNullOrWhiteSpace(translated)) continue;

                var bundleKey = EntityTranslation.BundleKey(nameof(MenuModule), m.Id.ToString(), nameof(MenuModule.Name));
                if (existing.Contains(bundleKey)) continue; // already present or hand-edited

                await _uow.Localization.AddEntityTranslationAsync(new EntityTranslation
                {
                    Id = Guid.NewGuid(),
                    EntityType = nameof(MenuModule),
                    EntityKey = m.Id.ToString(),
                    Field = nameof(MenuModule.Name),
                    LanguageId = language.Id,
                    Text = translated
                }, cancellationToken);
                written++;
            }
        }

        if (written > 0) await _uow.SaveChangesAsync(cancellationToken);
        return written;
    }

    public async Task<Dictionary<string, string>> ExportJsonAsync(Guid languageId, CancellationToken cancellationToken = default)
    {
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var language = languages.FirstOrDefault(l => l.Id == languageId)
            ?? throw new NotFoundException($"Language '{languageId}' was not found.");

        var code = (language.Code ?? string.Empty).ToLowerInvariant();
        var rows = await _uow.Localization.GetResourcesAsync(language.Id, cancellationToken);

        return rows
            .GroupBy(r => r.Namespace, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => $"{g.Key}/{g.Key}.{code}.json",
                g => System.Text.Json.JsonSerializer.Serialize(
                    g.OrderBy(r => r.Key, StringComparer.Ordinal).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal),
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }),
                StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Language> RequireEditableLanguageAsync(Guid languageId, CancellationToken cancellationToken)
    {
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var language = languages.FirstOrDefault(l => l.Id == languageId)
            ?? throw new NotFoundException($"Language '{languageId}' was not found.");

        if (IsBase(language))
            throw new BadRequestException("English is the base language and cannot be translated.");

        return language;
    }

    private static Language? FindBase(IEnumerable<Language> languages)
        => languages.FirstOrDefault(IsBase);

    private static bool IsBase(Language l)
        => string.Equals(l.Code, BaseCode, StringComparison.OrdinalIgnoreCase);

    private static string NamespaceOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot <= 0 ? "general" : key[..dot];
    }

    /// <summary>Route paths are compared without a leading slash or trailing junk.</summary>
    public static string NormaliseRoute(string route)
        => route.Trim().Trim('/').ToLowerInvariant();

    private static string VersionOf(int labelCount, int entityCount)
        => $"{labelCount}.{entityCount}";

    /// <summary>Top-level modules each followed by their children; nothing is ever dropped.</summary>
    private static IEnumerable<MenuModule> OrderForDisplay(IReadOnlyList<MenuModule> modules)
    {
        var emitted = new HashSet<Guid>();
        foreach (var root in modules.Where(m => m.ParentId is null).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name))
        {
            if (emitted.Add(root.Id)) yield return root;
            foreach (var child in modules.Where(m => m.ParentId == root.Id).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name))
                if (emitted.Add(child.Id)) yield return child;
        }
        foreach (var rest in modules.Where(m => !emitted.Contains(m.Id)).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name))
            yield return rest;
    }
}

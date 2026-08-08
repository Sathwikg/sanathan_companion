using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

/// <inheritdoc />
public sealed class TranslationCatalog : ITranslationCatalog
{
    private const string BaseCode = "en";

    private readonly IServiceScopeFactory _scopeFactory;

    // Snapshots are immutable, so readers never lock. A rebuild swaps the whole entry.
    private readonly ConcurrentDictionary<string, Lazy<Task<TranslationSnapshot?>>> _byCode =
        new(StringComparer.OrdinalIgnoreCase);

    public TranslationCatalog(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public Task<TranslationSnapshot?> GetAsync(string? languageCode, CancellationToken cancellationToken = default)
    {
        var code = (languageCode ?? string.Empty).Trim().ToLowerInvariant();

        // English is the source text — never translated, and costs nothing.
        if (code.Length == 0 || code == BaseCode) return Task.FromResult<TranslationSnapshot?>(null);

        // Lazy ensures a burst of concurrent first-requests builds the snapshot once, not N times.
        var lazy = _byCode.GetOrAdd(code, c => new Lazy<Task<TranslationSnapshot?>>(
            () => BuildAsync(c, CancellationToken.None), LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    public void Invalidate() => _byCode.Clear();

    private async Task<TranslationSnapshot?> BuildAsync(string code, CancellationToken cancellationToken)
    {
        // The catalog is a singleton but the repositories are scoped, so open our own scope.
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var language = (await uow.Languages.GetAllOrderedAsync(cancellationToken))
            .FirstOrDefault(l => l.IsActive && string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        if (language is null) return null;

        // ---- per-row overrides ----
        var entities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in await uow.Localization.GetEntityTranslationsAsync(language.Id, cancellationToken))
            if (!string.IsNullOrWhiteSpace(t.Text))
                entities[EntityTranslation.BundleKey(t.EntityType, t.EntityKey, t.Field)] = t.Text;

        // ---- shared dictionary ----
        var translated = await uow.Localization.GetTermsForLanguageAsync(language.Id, cancellationToken);

        var wholeValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var allTerms = new Dictionary<string, string>(StringComparer.Ordinal);
        var byCategory = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (term, text) in translated)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            wholeValues[TermMatcher.NormaliseKey(term.Source)] = text!;
            allTerms[term.Source] = text!;

            var category = string.IsNullOrWhiteSpace(term.Category) ? "general" : term.Category;
            if (!byCategory.TryGetValue(category, out var bucket))
                byCategory[category] = bucket = new Dictionary<string, string>(StringComparer.Ordinal);
            bucket[term.Source] = text!;
        }

        // Compiling the regexes is the expensive part — done here, once per invalidation.
        var matchers = byCategory.ToDictionary(
            kv => kv.Key,
            kv => new TermMatcher(kv.Value),
            StringComparer.OrdinalIgnoreCase);

        // ---- per-form opt-out ----
        var modules = await uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var disabledIds = (await uow.Localization.GetFormConfigsAsync(language.Id, cancellationToken))
            .Where(c => !c.Enabled)
            .Select(c => c.MenuModuleId)
            .ToHashSet();

        var disabledRoutes = modules
            .Where(m => disabledIds.Contains(m.Id) && !string.IsNullOrWhiteSpace(m.RoutePath))
            .Select(m => TranslationSnapshot.NormaliseRoute(m.RoutePath!))
            .ToHashSet(StringComparer.Ordinal);

        return new TranslationSnapshot(
            language.Id,
            code,
            entities,
            wholeValues,
            matchers,
            new TermMatcher(allTerms),
            disabledRoutes);
    }
}

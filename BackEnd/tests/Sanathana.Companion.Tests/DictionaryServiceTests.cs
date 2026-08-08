using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Behaviour of the shared term dictionary — the vocabulary that translates database text — against
/// a real (in-memory) database.
/// </summary>
/// <remarks>
/// <para>What these protect, and why:</para>
/// <list type="bullet">
/// <item>The grid is the admin's only worklist, so the paging/filter/ordering contract has to hold
/// exactly: a wrong <c>TotalCount</c> or an unclamped page silently hides terms that will never get
/// translated, and runtime misses must float to the top because those are what users are hitting.</item>
/// <item>English is the source text, never a translation. A row must expose one editable value per
/// active non-base language and nothing for English; a regression here would let an admin overwrite
/// the source and break every fallback that depends on it.</item>
/// <item>Blanking a value must DELETE the row rather than store an empty string — an empty row would
/// win the resolution chain and render blank instead of falling back to English.</item>
/// <item>Hand-edit protection: <see cref="TranslationTermText.IsSeeded"/> flips to false on a manual
/// save so the next deploy's import leaves it alone. This is the one guarantee an admin cannot
/// recover from if it breaks — their work would vanish on release day.</item>
/// </list>
/// </remarks>
public class DictionaryServiceTests
{
    /// <summary>The shipped vocabulary, stood up by the test instead of read from embedded files.</summary>
    private sealed class FakeVocabulary : ITermVocabularySource
    {
        private readonly IReadOnlyList<(string Source, string Category)> _terms;
        private readonly IReadOnlyDictionary<string, Dictionary<string, string>> _translations;

        public FakeVocabulary(
            IReadOnlyList<(string Source, string Category)> terms,
            IReadOnlyDictionary<string, Dictionary<string, string>>? translations = null)
        {
            _terms = terms;
            _translations = translations ?? new Dictionary<string, Dictionary<string, string>>();
        }

        public IReadOnlyList<(string Source, string Category)> Terms() => _terms;
        public IReadOnlyDictionary<string, Dictionary<string, string>> Translations() => _translations;
    }

    /// <summary>Records cache drops. The real catalog is a singleton, so a missed drop serves stale text.</summary>
    private sealed class CountingCatalog : ITranslationCatalog
    {
        public int Invalidations { get; private set; }

        public Task<TranslationSnapshot?> GetAsync(string? languageCode, CancellationToken cancellationToken = default)
            => Task.FromResult<TranslationSnapshot?>(null);

        public void Invalidate() => Invalidations++;
    }

    /// <summary>Ids from <see cref="SeedGridAsync"/>, so a test can address one column or one term.</summary>
    private sealed record Fixture(
        Guid English,
        Guid Telugu,
        Guid Hindi,
        TranslationTerm Amavasya,
        TranslationTerm Bhakti,
        TranslationTerm Chandra);

    private static async Task<(Guid English, Guid Telugu)> SeedLanguagesAsync(TestHarness harness)
    {
        var en = new Language { Id = Guid.NewGuid(), Name = "English", Code = "en", IsActive = true };
        var te = new Language { Id = Guid.NewGuid(), Name = "Telugu", Code = "te", NativeName = "తెలుగు", IsActive = true };
        harness.Context.Set<Language>().AddRange(en, te);
        await harness.Context.SaveChangesAsync();
        return (en.Id, te.Id);
    }

    private static async Task<TranslationTerm> AddTermAsync(
        TestHarness harness,
        string source,
        string category,
        int missCount,
        TermOrigin origin,
        Dictionary<Guid, string>? texts = null)
    {
        var term = new TranslationTerm
        {
            Id = Guid.NewGuid(),
            TermKey = TranslationTerm.Normalise(source),
            Source = source,
            Category = category,
            Origin = origin,
            MissCount = missCount,
            IsActive = true
        };
        harness.Context.Set<TranslationTerm>().Add(term);

        foreach (var (languageId, text) in texts ?? new Dictionary<Guid, string>())
        {
            harness.Context.Set<TranslationTermText>().Add(new TranslationTermText
            {
                Id = Guid.NewGuid(),
                TermId = term.Id,
                LanguageId = languageId,
                Text = text,
                IsSeeded = true
            });
        }

        await harness.Context.SaveChangesAsync();
        return term;
    }

    /// <summary>
    /// Three terms across two categories and two translatable languages: one complete, one partly
    /// translated with a high runtime miss count, one untouched. Also seeds an inactive language,
    /// which must never become a column.
    /// </summary>
    private static async Task<Fixture> SeedGridAsync(TestHarness harness)
    {
        var en = new Language { Id = Guid.NewGuid(), Name = "English", Code = "en", IsActive = true };
        var te = new Language { Id = Guid.NewGuid(), Name = "Telugu", Code = "te", NativeName = "తెలుగు", IsActive = true };
        var hi = new Language { Id = Guid.NewGuid(), Name = "Hindi", Code = "hi", NativeName = "हिन्दी", IsActive = true };
        var retired = new Language { Id = Guid.NewGuid(), Name = "Kannada", Code = "kn", IsActive = false };
        harness.Context.Set<Language>().AddRange(en, te, hi, retired);
        await harness.Context.SaveChangesAsync();

        var amavasya = await AddTermAsync(harness, "Amavasya", "panchangam", 0, TermOrigin.Seeded,
            new Dictionary<Guid, string> { [te.Id] = "అమావాస్య", [hi.Id] = "अमावस्या" });
        var bhakti = await AddTermAsync(harness, "Bhakti", "general", 7, TermOrigin.RuntimeMiss,
            new Dictionary<Guid, string> { [te.Id] = "భక్తి" });
        var chandra = await AddTermAsync(harness, "Chandra", "panchangam", 0, TermOrigin.Harvested);

        return new Fixture(en.Id, te.Id, hi.Id, amavasya, bhakti, chandra);
    }

    private static SaveDictionaryDto Save(Guid termId, Guid languageId, string value)
        => new() { Rows = { new SaveDictionaryRowDto { TermId = termId, Values = { [languageId] = value } } } };

    private static TranslationTermText? RowFor(TestHarness harness, string termKey, Guid languageId)
    {
        var term = harness.Context.Set<TranslationTerm>().Single(t => t.TermKey == termKey);
        return harness.Context.Set<TranslationTermText>()
            .FirstOrDefault(x => x.TermId == term.Id && x.LanguageId == languageId);
    }

    // ---- The grid ----

    [Fact]
    public async Task Grid_has_one_editable_value_per_active_non_base_language()
    {
        using var harness = new TestHarness();
        var f = await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var page = await service.GetPageAsync(null, null, false, 1, 50);

        // English leads as the read-only source column; the inactive Kannada is not offered at all.
        Assert.Equal(new[] { "en", "hi", "te" }, page.Languages.Select(l => l.Code));
        Assert.True(page.Languages[0].IsBase, "English must be flagged as the base/source column.");
        Assert.All(page.Languages.Skip(1), l => Assert.False(l.IsBase));
        Assert.Equal("తెలుగు", page.Languages.Single(l => l.Code == "te").NativeName);

        var amavasya = page.Rows.Single(r => r.Source == "Amavasya");
        Assert.Equal(2, amavasya.Values.Count);
        Assert.False(amavasya.Values.ContainsKey(f.English),
            "English is the source text, so it must never appear as an editable column.");
        Assert.Equal("అమావాస్య", amavasya.Values[f.Telugu]);
        Assert.Equal("अमावस्या", amavasya.Values[f.Hindi]);

        // An untranslated language shows blank rather than being absent, so the grid stays rectangular.
        var chandra = page.Rows.Single(r => r.Source == "Chandra");
        Assert.Equal(string.Empty, chandra.Values[f.Telugu]);
        Assert.Equal(string.Empty, chandra.Values[f.Hindi]);
    }

    [Fact]
    public async Task Deactivated_terms_are_kept_out_of_the_grid()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);

        var retired = await AddTermAsync(harness, "Obsolete", "general", 0, TermOrigin.Harvested);
        retired.IsActive = false;
        await harness.Context.SaveChangesAsync();

        var page = await new DictionaryService(harness.UnitOfWork).GetPageAsync(null, null, false, 1, 50);

        Assert.DoesNotContain(page.Rows, r => r.Source == "Obsolete");
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.MissingCount);   // the retired term must not inflate the outstanding work
    }

    [Fact]
    public async Task Missing_count_counts_terms_lacking_at_least_one_language()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var all = await service.GetPageAsync(null, null, false, 1, 50);

        // Bhakti has no Hindi, Chandra has nothing; Amavasya is complete.
        Assert.Equal(2, all.MissingCount);

        // It is the size of the whole job, so narrowing the view must not shrink it.
        var narrowed = await service.GetPageAsync("general", null, false, 1, 50);
        Assert.Equal(1, narrowed.TotalCount);
        Assert.Equal(2, narrowed.MissingCount);
    }

    [Fact]
    public async Task Rows_are_ordered_with_runtime_misses_first()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);

        var page = await new DictionaryService(harness.UnitOfWork).GetPageAsync(null, null, false, 1, 50);

        // Bhakti has 7 misses so it outranks alphabetical order; the rest fall back to A-Z.
        Assert.Equal(new[] { "Bhakti", "Amavasya", "Chandra" }, page.Rows.Select(r => r.Source));
        Assert.Equal(7, page.Rows[0].MissCount);
        Assert.Equal(nameof(TermOrigin.RuntimeMiss), page.Rows[0].Origin);
    }

    [Fact]
    public async Task Total_count_covers_the_whole_filtered_set_not_just_the_page()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var first = await service.GetPageAsync(null, null, false, page: 1, pageSize: 2);
        Assert.Equal(2, first.Rows.Count);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(new[] { "Bhakti", "Amavasya" }, first.Rows.Select(r => r.Source));

        var second = await service.GetPageAsync(null, null, false, page: 2, pageSize: 2);
        Assert.Equal(new[] { "Chandra" }, second.Rows.Select(r => r.Source));
        Assert.Equal(3, second.TotalCount);

        // With a filter, the total is the filtered total — not the unfiltered one, not the page.
        var filtered = await service.GetPageAsync("panchangam", null, false, page: 1, pageSize: 1);
        Assert.Equal(2, filtered.TotalCount);
        Assert.Single(filtered.Rows);
    }

    [Fact]
    public async Task Page_size_and_page_number_are_clamped()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var tiny = await service.GetPageAsync(null, null, false, page: 1, pageSize: 0);
        Assert.Equal(1, tiny.PageSize);
        Assert.Single(tiny.Rows);

        var huge = await service.GetPageAsync(null, null, false, page: 1, pageSize: 9999);
        Assert.Equal(200, huge.PageSize);
        Assert.Equal(3, huge.Rows.Count);

        // Past the end lands on the last page rather than returning nothing.
        var pastEnd = await service.GetPageAsync(null, null, false, page: 99, pageSize: 2);
        Assert.Equal(2, pastEnd.Page);
        Assert.Equal(new[] { "Chandra" }, pastEnd.Rows.Select(r => r.Source));

        var beforeStart = await service.GetPageAsync(null, null, false, page: -5, pageSize: 2);
        Assert.Equal(1, beforeStart.Page);
        Assert.Equal("Bhakti", beforeStart.Rows[0].Source);

        // An empty result still reports a valid page rather than dividing by zero.
        var empty = await service.GetPageAsync("no-such-category", null, false, page: 4, pageSize: 10);
        Assert.Equal(1, empty.Page);
        Assert.Equal(0, empty.TotalCount);
        Assert.Empty(empty.Rows);
    }

    [Fact]
    public async Task Filtering_by_category_narrows_the_rows_but_not_the_category_list()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var page = await service.GetPageAsync("PANCHANGAM", null, false, 1, 50);   // case-insensitive

        Assert.Equal(new[] { "Amavasya", "Chandra" }, page.Rows.Select(r => r.Source));
        Assert.Equal(2, page.TotalCount);

        // The picker must keep offering every category, otherwise the admin cannot switch back.
        Assert.Equal(new[] { "general", "panchangam" }, page.Categories);
    }

    [Fact]
    public async Task Search_matches_the_english_source_and_the_translated_text()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var bySource = await service.GetPageAsync(null, "  chand ", false, 1, 50);   // trimmed, case-insensitive, partial
        Assert.Equal(new[] { "Chandra" }, bySource.Rows.Select(r => r.Source));

        // Searching the Telugu text finds the row even though the English does not contain it.
        var byTranslation = await service.GetPageAsync(null, "భక్తి", false, 1, 50);
        Assert.Equal(new[] { "Bhakti" }, byTranslation.Rows.Select(r => r.Source));

        var nothing = await service.GetPageAsync(null, "zzz", false, 1, 50);
        Assert.Empty(nothing.Rows);
        Assert.Equal(0, nothing.TotalCount);
    }

    [Fact]
    public async Task Missing_only_keeps_the_incomplete_rows()
    {
        using var harness = new TestHarness();
        await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        var page = await service.GetPageAsync(null, null, missingOnly: true, 1, 50);

        // Bhakti lacks Hindi, Chandra lacks both; Amavasya is complete and drops out.
        Assert.Equal(new[] { "Bhakti", "Chandra" }, page.Rows.Select(r => r.Source));
        Assert.Equal(2, page.TotalCount);

        // Combines with the other filters rather than replacing them.
        var withCategory = await service.GetPageAsync("panchangam", null, missingOnly: true, 1, 50);
        Assert.Equal(new[] { "Chandra" }, withCategory.Rows.Select(r => r.Source));
    }

    // ---- Saving ----

    [Fact]
    public async Task Saving_writes_a_new_translation_and_updates_an_existing_one()
    {
        using var harness = new TestHarness();
        var f = await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        await service.SaveAsync(Save(f.Chandra.Id, f.Telugu, "  చంద్ర  "));   // brand new, and padded
        await service.SaveAsync(Save(f.Amavasya.Id, f.Telugu, "అమావాస్య-2")); // overwrite an existing row

        var added = RowFor(harness, "chandra", f.Telugu);
        Assert.NotNull(added);
        Assert.Equal("చంద్ర", added!.Text);   // stored trimmed
        Assert.False(added.IsSeeded, "A hand-typed value must be marked as no longer seed-owned.");

        var updated = RowFor(harness, "amavasya", f.Telugu);
        Assert.Equal("అమావాస్య-2", updated!.Text);
        Assert.False(updated.IsSeeded);

        // Only the edited language moved; Hindi is untouched.
        Assert.Equal("अमावस्या", RowFor(harness, "amavasya", f.Hindi)!.Text);

        var page = await service.GetPageAsync(null, null, false, 1, 50);
        Assert.Equal("చంద్ర", page.Rows.Single(r => r.Source == "Chandra").Values[f.Telugu]);
        Assert.Equal(2, page.MissingCount);   // Amavasya is now complete; Bhakti and Chandra still lack Hindi
    }

    [Fact]
    public async Task Blanking_a_translation_removes_the_row_so_it_falls_back_to_english()
    {
        using var harness = new TestHarness();
        var f = await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        await service.SaveAsync(Save(f.Amavasya.Id, f.Telugu, "   "));   // whitespace counts as blank

        // The row is gone rather than stored empty: an empty row would win the resolution chain and
        // render as blank text instead of falling back to the English source.
        Assert.Null(RowFor(harness, "amavasya", f.Telugu));
        Assert.NotNull(RowFor(harness, "amavasya", f.Hindi));

        harness.Context.ChangeTracker.Clear();   // read it back as a fresh request would
        var page = await service.GetPageAsync(null, null, false, 1, 50);
        Assert.Equal(string.Empty, page.Rows.Single(r => r.Source == "Amavasya").Values[f.Telugu]);
        Assert.Equal(3, page.MissingCount);
    }

    [Fact]
    public async Task Saving_the_english_column_is_ignored()
    {
        using var harness = new TestHarness();
        var f = await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        await service.SaveAsync(new SaveDictionaryDto
        {
            Rows =
            {
                new SaveDictionaryRowDto
                {
                    TermId = f.Chandra.Id,
                    Values = { [f.English] = "Moon", [f.Telugu] = "చంద్ర" }
                }
            }
        });

        // English is the source text: the attempt is skipped silently, not rejected...
        Assert.Empty(harness.Context.Set<TranslationTermText>().Where(x => x.LanguageId == f.English));
        Assert.Equal("Chandra", harness.Context.Set<TranslationTerm>().Single(t => t.TermKey == "chandra").Source);

        // ...and the rest of the same row still saves.
        Assert.Equal("చంద్ర", RowFor(harness, "chandra", f.Telugu)!.Text);
    }

    [Fact]
    public async Task Saving_rejects_an_unknown_term_or_language()
    {
        using var harness = new TestHarness();
        var f = await SeedGridAsync(harness);
        var service = new DictionaryService(harness.UnitOfWork);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.SaveAsync(Save(Guid.NewGuid(), f.Telugu, "ఏదో")));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.SaveAsync(Save(f.Chandra.Id, Guid.NewGuid(), "ఏదో")));

        // Validation happens before any write, so a bad batch leaves the table alone.
        Assert.Null(RowFor(harness, "chandra", f.Telugu));
    }

    [Fact]
    public async Task Saving_drops_the_translation_cache()
    {
        using var harness = new TestHarness();
        var f = await SeedGridAsync(harness);
        var catalog = new CountingCatalog();
        var service = new DictionaryService(harness.UnitOfWork, catalog);

        await service.SaveAsync(Save(f.Chandra.Id, f.Telugu, "చంద్ర"));

        // The catalog is a singleton; without this the app keeps serving the pre-edit snapshot.
        Assert.Equal(1, catalog.Invalidations);
    }

    // ---- Seeding the shipped vocabulary ----

    [Fact]
    public async Task Seeding_terms_is_idempotent_and_normalises_the_lookup_key()
    {
        using var harness = new TestHarness();
        var catalog = new CountingCatalog();
        var vocabulary = new FakeVocabulary(new[]
        {
            ("Navami", "panchangam"),
            ("  navami ", "panchangam"),        // same term, different spacing/case
            ("  Sukla   Paksha ", "panchangam"),
            ("   ", "general")                  // nothing to key on
        });
        var seeder = new TermSeedService(harness.UnitOfWork, vocabulary, catalog);

        Assert.Equal(2, await seeder.SeedTermsAsync());
        Assert.Equal(0, await seeder.SeedTermsAsync());   // re-running a deploy adds nothing

        var terms = harness.Context.Set<TranslationTerm>().ToList();
        Assert.Equal(2, terms.Count);
        Assert.Equal(new[] { "navami", "sukla paksha" },
            terms.Select(t => t.TermKey).OrderBy(k => k, StringComparer.Ordinal));

        var sukla = terms.Single(t => t.TermKey == "sukla paksha");
        Assert.Equal("Sukla   Paksha", sukla.Source);   // the source keeps its own spelling; only the key is collapsed
        Assert.Equal(TermOrigin.Seeded, sukla.Origin);
        Assert.True(sukla.IsActive);

        // The second run wrote nothing, so it must not have thrown the cache away either.
        Assert.Equal(1, catalog.Invalidations);
    }

    [Fact]
    public async Task Import_ignores_unknown_languages_unknown_terms_and_blank_text()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);

        var vocabulary = new FakeVocabulary(
            new[] { ("Navami", "panchangam"), ("Dasami", "panchangam") },
            new Dictionary<string, Dictionary<string, string>>
            {
                ["te"] = new()
                {
                    ["Navami"] = "నవమి",
                    ["Dasami"] = "   ",          // nothing shipped yet
                    ["Poornima"] = "పౌర్ణమి"      // not a term in the dictionary
                },
                ["zz"] = new() { ["Navami"] = "never" }   // no such language
            });

        var seeder = new TermSeedService(harness.UnitOfWork, vocabulary);
        await seeder.SeedTermsAsync();

        Assert.Equal(1, await seeder.ImportTermTranslationsAsync());
        Assert.Equal("నవమి", RowFor(harness, "navami", te)!.Text);
        Assert.Null(RowFor(harness, "dasami", te));
        Assert.Single(harness.Context.Set<TranslationTermText>());
    }

    [Fact]
    public async Task A_hand_edited_translation_is_not_clobbered_by_a_later_import()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);

        var shipped = new[] { ("Navami", "panchangam"), ("Dasami", "panchangam") };
        var first = new TermSeedService(harness.UnitOfWork, new FakeVocabulary(shipped,
            new Dictionary<string, Dictionary<string, string>>
            {
                ["te"] = new() { ["Navami"] = "నవమి", ["Dasami"] = "దశమి" }
            }));

        Assert.Equal(2, await first.SeedTermsAsync());
        Assert.Equal(2, await first.ImportTermTranslationsAsync());
        Assert.True(RowFor(harness, "navami", te)!.IsSeeded, "A freshly imported value is still seed-owned.");

        // An admin corrects one of them by hand.
        var navami = harness.Context.Set<TranslationTerm>().Single(t => t.TermKey == "navami");
        await new DictionaryService(harness.UnitOfWork).SaveAsync(Save(navami.Id, te, "నవమి (సరిచేసినది)"));
        Assert.False(RowFor(harness, "navami", te)!.IsSeeded,
            "Saving by hand must release the row from the seed, or the next deploy will overwrite it.");

        // The next release ships different text for both terms.
        var second = new TermSeedService(harness.UnitOfWork, new FakeVocabulary(shipped,
            new Dictionary<string, Dictionary<string, string>>
            {
                ["te"] = new() { ["Navami"] = "నవమి-v2", ["Dasami"] = "దశమి-v2" }
            }));

        Assert.Equal(0, await second.SeedTermsAsync());          // the terms already exist
        Assert.Equal(1, await second.ImportTermTranslationsAsync());

        Assert.Equal("నవమి (సరిచేసినది)", RowFor(harness, "navami", te)!.Text);   // the hand edit survives
        Assert.Equal("దశమి-v2", RowFor(harness, "dasami", te)!.Text);            // the seed-owned row refreshes
    }
}

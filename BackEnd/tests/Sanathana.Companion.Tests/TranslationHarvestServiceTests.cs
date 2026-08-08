using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Behaviour of the self-maintaining half of the dictionary: <see cref="TranslationHarvestService"/>,
/// which turns registered database columns and runtime translation misses into untranslated terms,
/// and <see cref="TranslationMissLog"/>, which is where the running app parks what it could not
/// translate.
/// </summary>
/// <remarks>
/// <para>These tests protect the rules that decide what is <i>vocabulary</i> and what is not. Getting
/// that wrong is not a crash, it is a slow poisoning: harvest a clock time or a month abbreviation
/// and the admin's worklist fills up with entries that must never be translated, and a term such as
/// "22" or "a" would additionally start matching inside dates and times all over the app. The
/// filters under test — digits are never words, month abbreviations are stopped, a term needs a
/// letter and two characters, and a bare word already covered by a phrase is skipped — are the only
/// thing standing between a hundred and fifty useful terms and several thousand useless ones.</para>
/// <para>The database is the real one (EF Core InMemory via <see cref="TestHarness"/>) with real
/// repositories, so idempotency is exercised against genuine persistence. Only the column reader is
/// faked: the real one needs live SQL and the EF model, and what matters here is what the harvester
/// does with the values, not how they are fetched. <c>HarvestMode</c>, the stop word list and the
/// word regex are private to the service, so every one of them is exercised through the single
/// public entry point <c>HarvestAsync</c>.</para>
/// </remarks>
public class TranslationHarvestServiceTests
{
    /// <summary>Canned column values, standing in for the SQL-backed reader.</summary>
    private sealed class FakeColumnReader : IVocabularyColumnReader
    {
        private readonly Dictionary<string, string[]> _values = new(StringComparer.Ordinal);

        public FakeColumnReader(params (string Table, string Column, string[] Values)[] columns)
        {
            foreach (var column in columns) _values[Key(column.Table, column.Column)] = column.Values;
        }

        /// <summary>Every "Table.Column" the harvester asked for, in order.</summary>
        public List<string> Requests { get; } = new();

        /// <summary>The <c>max</c> argument of the last read — the source's MaxDistinct safety valve.</summary>
        public int LastMax { get; private set; } = -1;

        public Task<IReadOnlyList<string>> ReadDistinctAsync(
            string tableName, string columnName, int max, CancellationToken cancellationToken = default)
        {
            Requests.Add(Key(tableName, columnName));
            LastMax = max;

            // Mirrors the real reader: an unregistered table/column yields nothing, and the cap is applied.
            var found = _values.TryGetValue(Key(tableName, columnName), out var values)
                ? values.Take(max).ToList()
                : new List<string>();

            return Task.FromResult<IReadOnlyList<string>>(found);
        }

        private static string Key(string table, string column) => $"{table}.{column}";
    }

    /// <summary>Stands in for the embedded shipped vocabulary, which is an Infrastructure concern.</summary>
    private sealed class FakeTermSeed : ITermSeedService
    {
        public int ImportCalls { get; private set; }
        public int ImportResult { get; init; }

        public Task<int> SeedTermsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ImportTermTranslationsAsync(CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            return Task.FromResult(ImportResult);
        }
    }

    /// <summary>Records cache invalidation; the real catalog is a singleton with live snapshots.</summary>
    private sealed class FakeCatalog : ITranslationCatalog
    {
        public int Invalidations { get; private set; }

        public Task<TranslationSnapshot?> GetAsync(string? languageCode, CancellationToken cancellationToken = default)
            => Task.FromResult<TranslationSnapshot?>(null);

        public void Invalidate() => Invalidations++;
    }

    // ---------------------------------------------------------------- fixture helpers

    private static TranslationHarvestService NewService(
        TestHarness harness,
        FakeColumnReader reader,
        ITranslationMissLog? misses = null,
        ITermSeedService? seed = null,
        ITranslationCatalog? catalog = null)
        => new(harness.UnitOfWork, reader, misses ?? new TranslationMissLog(), seed ?? new FakeTermSeed(), catalog);

    private static TranslationSource Source(
        string table, string column, HarvestMode mode, string category = "test", int maxDistinct = 5000, bool active = true)
        => new()
        {
            Id = Guid.NewGuid(),
            TableName = table,
            ColumnName = column,
            Category = category,
            Mode = mode,
            MaxDistinct = maxDistinct,
            IsActive = active
        };

    /// <summary>
    /// Replaces the two dozen sources shipped by HasData with exactly the ones a test cares about,
    /// so a test never depends on the production source list.
    /// </summary>
    private static async Task UseSourcesAsync(TestHarness harness, params TranslationSource[] sources)
    {
        harness.Context.TranslationSources.RemoveRange(harness.Context.TranslationSources.ToList());
        harness.Context.TranslationSources.AddRange(sources);
        await harness.Context.SaveChangesAsync();
    }

    /// <summary>Terms that already exist before a harvest runs, as an admin-entered dictionary would.</summary>
    private static async Task ExistingTermsAsync(TestHarness harness, params string[] sources)
    {
        foreach (var source in sources)
        {
            harness.Context.TranslationTerms.Add(new TranslationTerm
            {
                Id = Guid.NewGuid(),
                TermKey = TermMatcher.NormaliseKey(source),
                Source = source,
                Category = "test",
                Origin = TermOrigin.Manual,
                IsActive = true
            });
        }
        await harness.Context.SaveChangesAsync();
    }

    private static List<TranslationTerm> HarvestedTerms(TestHarness harness)
        => harness.Context.TranslationTerms.Where(t => t.Origin != TermOrigin.Manual).ToList();

    /// <summary>Keys of everything the harvest created, ordered so a test can compare them literally.</summary>
    private static string[] HarvestedKeys(TestHarness harness)
        => HarvestedTerms(harness).Select(t => t.TermKey).OrderBy(k => k, StringComparer.Ordinal).ToArray();

    // ---------------------------------------------------------------- WholeValue mode

    [Fact]
    public async Task WholeValue_mode_turns_each_distinct_value_into_one_term()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Days", "Name", HarvestMode.WholeValue, category: "day"));
        var reader = new FakeColumnReader(("Days", "Name", new[] { "Monday", "Tuesday", "  monday " }));

        var result = await NewService(harness, reader).HarvestAsync();

        // "  monday " normalises onto "monday", so three distinct column values are two terms.
        Assert.Equal(2, result.Added);
        Assert.Equal(new[] { "monday", "tuesday" }, HarvestedKeys(harness));

        var monday = HarvestedTerms(harness).Single(t => t.TermKey == "monday");
        Assert.Equal("Monday", monday.Source);       // the English the admin edits against, not the key
        Assert.Equal("day", monday.Category);        // inherited from the source row
        Assert.Equal(TermOrigin.Harvested, monday.Origin);
        Assert.True(monday.IsActive, "A freshly harvested term must be active or it never reaches the matcher.");
        Assert.Equal(0, monday.MissCount);
    }

    [Fact]
    public async Task A_source_is_reported_with_its_counts_and_scanned_under_its_own_cap()
    {
        using var harness = new TestHarness();
        await ExistingTermsAsync(harness, "Monday");   // already in the dictionary
        await UseSourcesAsync(harness, Source("Days", "Name", HarvestMode.WholeValue, maxDistinct: 3));
        var reader = new FakeColumnReader(
            ("Days", "Name", new[] { "Monday", "Tuesday", "Wednesday", "Thursday" }));  // fourth is past the cap

        var result = await NewService(harness, reader).HarvestAsync();

        Assert.Equal(3, reader.LastMax);   // MaxDistinct reached the reader rather than being ignored

        var days = Assert.Single(result.BySource);
        Assert.Equal("Days", days.TableName);
        Assert.Equal("Name", days.ColumnName);
        Assert.Equal(3, days.DistinctValues);   // Thursday was never read
        Assert.Equal(2, days.NewTerms);         // Monday was already known

        Assert.Equal(2, result.Added);
        Assert.Equal(new[] { "tuesday", "wednesday" }, HarvestedKeys(harness));
    }

    [Fact]
    public async Task Inactive_sources_are_never_scanned()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(
            harness,
            Source("Days", "Name", HarvestMode.WholeValue),
            Source("Regions", "Name", HarvestMode.WholeValue, active: false));
        var reader = new FakeColumnReader(
            ("Days", "Name", new[] { "Monday" }),
            ("Regions", "Name", new[] { "Telangana" }));

        var result = await NewService(harness, reader).HarvestAsync();

        Assert.Equal(new[] { "Days.Name" }, reader.Requests);
        Assert.Equal(new[] { "monday" }, HarvestedKeys(harness));
        Assert.Single(result.BySource);
    }

    // ---------------------------------------------------------------- Words mode

    [Fact]
    public async Task Words_mode_splits_a_composite_and_never_yields_a_digit_or_a_time()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Panchangams", "TithiDetails", HarvestMode.Words, category: "panchangam"));
        var reader = new FakeColumnReader(
            ("Panchangams", "TithiDetails", new[] { "Navami upto 00:35, 22 Dec, Dasami from 00:36, 22 Dec" }));

        await NewService(harness, reader).HarvestAsync();

        // Vocabulary only: the clock times, the day number and the month abbreviation are not words.
        Assert.Equal(new[] { "dasami", "from", "navami", "upto" }, HarvestedKeys(harness));

        foreach (var key in HarvestedKeys(harness))
        {
            Assert.True(key.All(c => !char.IsDigit(c)),
                $"'{key}' contains a digit; a numeric term would corrupt clock times and dates at match time.");
            Assert.True(key.Any(char.IsLetter), $"'{key}' has no letter and must never have been harvested.");
        }
    }

    [Fact]
    public async Task Month_abbreviations_are_stopped_so_dates_stay_english()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Panchangams", "TithiDetails", HarvestMode.Words));
        var reader = new FakeColumnReader(("Panchangams", "TithiDetails", new[]
        {
            "Jan Feb Mar Apr May Jun Jul Aug Sep Sept Oct Nov Dec Amavasya"
        }));

        var result = await NewService(harness, reader).HarvestAsync();

        Assert.Equal(new[] { "amavasya" }, HarvestedKeys(harness));
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public async Task Words_mode_harvests_bare_words_when_no_phrase_covers_them()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Panchangams", "AmruthaKalam", HarvestMode.Words));
        var reader = new FakeColumnReader(("Panchangams", "AmruthaKalam", new[] { "full day", "Navami full day" }));

        await NewService(harness, reader).HarvestAsync();

        // Control for the next test: with nothing in the dictionary the bare words ARE added.
        Assert.Equal(new[] { "day", "full", "navami" }, HarvestedKeys(harness));
    }

    [Fact]
    public async Task Words_mode_skips_a_bare_word_already_covered_by_a_multi_word_term()
    {
        using var harness = new TestHarness();
        await ExistingTermsAsync(harness, "full day");
        await UseSourcesAsync(harness, Source("Panchangams", "AmruthaKalam", HarvestMode.Words));
        var reader = new FakeColumnReader(("Panchangams", "AmruthaKalam", new[] { "full day", "Navami full day" }));

        var result = await NewService(harness, reader).HarvestAsync();

        // The phrase already translates both words, so "full" and "day" would be dead weight.
        Assert.Equal(new[] { "navami" }, HarvestedKeys(harness));
        Assert.Equal(1, result.Added);
    }

    // ---------------------------------------------------------------- candidate filtering

    [Fact]
    public async Task Values_shorter_than_two_characters_or_with_no_letter_are_rejected()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Feedbacks", "Status", HarvestMode.WholeValue));
        var reader = new FakeColumnReader(("Feedbacks", "Status", new[] { "a", "42", "00:35", "--", "   ", "Om" }));

        var result = await NewService(harness, reader).HarvestAsync();

        Assert.Equal(new[] { "om" }, HarvestedKeys(harness));
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public async Task Harvest_is_idempotent()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Days", "Name", HarvestMode.WholeValue));
        var reader = new FakeColumnReader(("Days", "Name", new[] { "Monday", "Tuesday" }));
        var catalog = new FakeCatalog();
        var service = NewService(harness, reader, catalog: catalog);

        var first = await service.HarvestAsync();
        var second = await service.HarvestAsync();

        Assert.Equal(2, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(2, HarvestedKeys(harness).Length);
        Assert.Equal(0, second.BySource.Single().NewTerms);
        Assert.Equal(1, catalog.Invalidations);   // a no-op harvest must not churn the singleton cache
    }

    // ---------------------------------------------------------------- runtime misses

    [Fact]
    public async Task Runtime_misses_become_terms_carrying_their_miss_count()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness); // nothing to scan: these values exist in no table at all
        var misses = new TranslationMissLog();
        misses.Record("Sunrise", "panchangam");
        misses.Record("Sunrise", "panchangam");
        misses.Record("Sunrise", "panchangam");
        misses.Record("Moonrise", null);

        var result = await NewService(harness, new FakeColumnReader(), misses).HarvestAsync();

        Assert.Equal(2, result.FromRuntimeMisses);
        Assert.Equal(2, result.Added);

        var sunrise = HarvestedTerms(harness).Single(t => t.TermKey == "sunrise");
        Assert.Equal(TermOrigin.RuntimeMiss, sunrise.Origin);
        Assert.Equal(3, sunrise.MissCount);        // ranks the admin's worklist by what users actually hit
        Assert.Equal("panchangam", sunrise.Category);

        var moonrise = HarvestedTerms(harness).Single(t => t.TermKey == "moonrise");
        Assert.Equal("general", moonrise.Category); // a miss with no category still has to land somewhere

        Assert.Equal(0, misses.Count);              // the harvest drained the log
    }

    [Fact]
    public async Task Runtime_misses_that_are_already_known_or_unusable_are_ignored()
    {
        using var harness = new TestHarness();
        await ExistingTermsAsync(harness, "Navami");
        await UseSourcesAsync(harness);
        var misses = new TranslationMissLog();
        misses.Record("Navami", "panchangam");  // already in the dictionary
        misses.Record("navami", "panchangam");  // same key, different casing
        misses.Record("42", null);              // no letter, so never a term
        misses.Record("Sunrise", "panchangam");

        var result = await NewService(harness, new FakeColumnReader(), misses).HarvestAsync();

        Assert.Equal(1, result.FromRuntimeMisses);
        Assert.Equal(new[] { "sunrise" }, HarvestedKeys(harness));
    }

    // ---------------------------------------------------------------- seeding and cache

    [Fact]
    public async Task A_harvest_that_added_terms_applies_shipped_translations_and_invalidates_the_catalog()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Days", "Name", HarvestMode.WholeValue));
        var reader = new FakeColumnReader(("Days", "Name", new[] { "Monday" }));
        var seed = new FakeTermSeed { ImportResult = 7 };
        var catalog = new FakeCatalog();

        var result = await NewService(harness, reader, seed: seed, catalog: catalog).HarvestAsync();

        // Startup seeding runs BEFORE the harvest, so terms discovered here would stay untranslated
        // even though the build ships their text — unless the import is re-run afterwards.
        Assert.Equal(1, seed.ImportCalls);
        Assert.Equal(7, result.SeededTranslations);
        Assert.Equal(1, catalog.Invalidations);
    }

    [Fact]
    public async Task A_harvest_that_found_nothing_does_not_reseed_or_invalidate()
    {
        using var harness = new TestHarness();
        await UseSourcesAsync(harness, Source("Days", "Name", HarvestMode.WholeValue));
        var reader = new FakeColumnReader(("Days", "Name", new[] { "42", "a" })); // nothing usable
        var seed = new FakeTermSeed { ImportResult = 7 };
        var catalog = new FakeCatalog();

        var result = await NewService(harness, reader, seed: seed, catalog: catalog).HarvestAsync();

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.SeededTranslations);
        Assert.Equal(0, seed.ImportCalls);
        Assert.Equal(0, catalog.Invalidations);
    }

    // ---------------------------------------------------------------- TranslationMissLog

    [Fact]
    public void Miss_log_counts_repeats_of_the_same_normalised_value()
    {
        var log = new TranslationMissLog();

        log.Record("Navami", "panchangam");
        log.Record("  navami ", "panchangam");   // same key: trimmed, collapsed, lower-invariant
        log.Record("NAVAMI", "panchangam");

        Assert.Equal(1, log.Count);

        var drained = Assert.Single(log.Drain());
        Assert.Equal(3, drained.Count);
        Assert.Equal("Navami", drained.Value);   // the first spelling seen, trimmed — what the admin edits
        Assert.Equal("panchangam", drained.Category);
    }

    [Fact]
    public void Drain_empties_the_log_and_returns_entries_ordered_by_count_descending()
    {
        var log = new TranslationMissLog();
        log.Record("rare", null);
        log.Record("common", null);
        log.Record("common", null);
        log.Record("common", null);
        log.Record("middling", null);
        log.Record("middling", null);

        var drained = log.Drain();

        // The order IS the admin's worklist: fix what users hit most first.
        Assert.Equal(new[] { "common", "middling", "rare" }, drained.Select(m => m.Value).ToArray());
        Assert.Equal(new[] { 3, 2, 1 }, drained.Select(m => m.Count).ToArray());

        Assert.Equal(0, log.Count);
        Assert.Empty(log.Drain());
    }

    [Fact]
    public void Miss_log_ignores_null_and_whitespace()
    {
        var log = new TranslationMissLog();

        log.Record(null!, "panchangam");
        log.Record(string.Empty, null);
        log.Record("   \t ", null);

        Assert.Equal(0, log.Count);
        Assert.Empty(log.Drain());
    }

    [Fact]
    public void Miss_log_is_capped_but_keeps_counting_the_keys_it_already_tracks()
    {
        // The cap is a private const (5000); it is asserted here through the public surface because
        // an unbounded log is a memory leak driven straight by response content.
        const int cap = 5_000;
        var log = new TranslationMissLog();

        for (var i = 0; i < cap; i++) log.Record($"value {i}", null);
        Assert.Equal(cap, log.Count);

        log.Record("one too many", null);
        Assert.Equal(cap, log.Count);

        // A key already tracked must still accumulate, or the ranking would freeze at the cap.
        log.Record("value 0", null);
        Assert.Equal(cap, log.Count);

        var drained = log.Drain();
        Assert.Equal(cap, drained.Count);
        Assert.DoesNotContain(drained, m => m.Value == "one too many");
        Assert.Equal(2, drained[0].Count);              // "value 0", now the most-hit entry
        Assert.Equal("value 0", drained[0].Value);
    }
}

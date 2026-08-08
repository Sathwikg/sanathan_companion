using Microsoft.Extensions.DependencyInjection;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Behavioural contract for <see cref="TranslationCatalog"/> and the immutable
/// <see cref="TranslationSnapshot"/> it hands out.
/// </summary>
/// <remarks>
/// <para>The catalog is the only thing standing between the response filter and a database round
/// trip per request, so three properties are load-bearing and each has a test here: English (and a
/// missing code) must short-circuit <b>before</b> a scope is ever opened; a built snapshot must be
/// reused until <see cref="TranslationCatalog.Invalidate"/> is called; and a burst of concurrent
/// first-requests must build exactly once. The scope counter on the fake factory is what makes the
/// first and third observable — assert on it and a regression that re-queries per request fails
/// loudly instead of silently costing I/O.</para>
/// <para>Translations are ASCII markers such as "[navami]" rather than real Telugu, matching
/// <see cref="TermMatcherTests"/>: the behaviour under test is the lookup plumbing, not the script,
/// and an ASCII-only file cannot fail because of how it was encoded on disk.</para>
/// <para><see cref="TranslationCatalog.BuildAsync"/> is private, so everything about how a snapshot
/// is assembled is asserted through <see cref="TranslationCatalog.GetAsync"/> and the snapshot's own
/// public surface.</para>
/// </remarks>
public class TranslationCatalogTests
{
    // ------------------------------------------------------------------ DI fakes

    /// <summary>
    /// Stands in for the container. The catalog is a singleton over scoped repositories, so it opens
    /// its own scope per build; counting those calls is how the tests observe whether a build
    /// actually happened.
    /// </summary>
    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IUnitOfWork _unitOfWork;
        private int _scopesCreated;

        public FakeScopeFactory(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        /// <summary>How many times the catalog went to the database.</summary>
        public int ScopesCreated => Volatile.Read(ref _scopesCreated);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _scopesCreated);
            return new FakeScope(new FakeProvider(_unitOfWork));
        }
    }

    private sealed class FakeScope : IServiceScope
    {
        public FakeScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

        public IServiceProvider ServiceProvider { get; }

        // Deliberately a no-op: the harness owns the DbContext and the tests keep using it after
        // the catalog has disposed its scope.
        public void Dispose() { }
    }

    private sealed class FakeProvider : IServiceProvider
    {
        private readonly IUnitOfWork _unitOfWork;

        public FakeProvider(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public object? GetService(Type serviceType)
            => serviceType == typeof(IUnitOfWork) ? _unitOfWork : null;
    }

    // ------------------------------------------------------------------ seeding helpers

    private static (TranslationCatalog Catalog, FakeScopeFactory Scopes) NewCatalog(TestHarness harness)
    {
        var scopes = new FakeScopeFactory(harness.UnitOfWork);
        return (new TranslationCatalog(scopes), scopes);
    }

    private static async Task<Language> AddLanguageAsync(
        TestHarness harness, string name, string code, bool isActive = true)
    {
        var language = new Language { Id = Guid.NewGuid(), Name = name, Code = code, IsActive = isActive };
        harness.Context.Set<Language>().Add(language);
        await harness.Context.SaveChangesAsync();
        return language;
    }

    /// <summary>Adds a vocabulary entry; <paramref name="text"/> null means "harvested, not yet translated".</summary>
    private static async Task AddTermAsync(
        TestHarness harness,
        Guid languageId,
        string source,
        string? text,
        string category = "general",
        bool isActive = true)
    {
        var term = new TranslationTerm
        {
            Id = Guid.NewGuid(),
            TermKey = TranslationTerm.Normalise(source),
            Source = source,
            Category = category,
            Origin = TermOrigin.Manual,
            IsActive = isActive
        };
        harness.Context.Set<TranslationTerm>().Add(term);

        if (text is not null)
        {
            harness.Context.Set<TranslationTermText>().Add(new TranslationTermText
            {
                Id = Guid.NewGuid(),
                TermId = term.Id,
                LanguageId = languageId,
                Text = text
            });
        }

        await harness.Context.SaveChangesAsync();
    }

    private static async Task AddEntityTranslationAsync(
        TestHarness harness, Guid languageId, string entityType, string entityKey, string field, string text)
    {
        harness.Context.Set<EntityTranslation>().Add(new EntityTranslation
        {
            Id = Guid.NewGuid(),
            LanguageId = languageId,
            EntityType = entityType,
            EntityKey = entityKey,
            Field = field,
            Text = text
        });
        await harness.Context.SaveChangesAsync();
    }

    /// <summary>Adds a form and opts it out of <paramref name="languageId"/>.</summary>
    private static async Task<MenuModule> AddDisabledFormAsync(
        TestHarness harness, Guid languageId, string routePath)
    {
        var module = new MenuModule
        {
            Id = Guid.NewGuid(),
            Name = $"Form {routePath}",
            RoutePath = routePath,
            DisplayOrder = 99
        };
        harness.Context.Set<MenuModule>().Add(module);
        harness.Context.Set<LanguageFormConfig>().Add(new LanguageFormConfig
        {
            Id = Guid.NewGuid(),
            LanguageId = languageId,
            MenuModuleId = module.Id,
            Enabled = false
        });
        await harness.Context.SaveChangesAsync();
        return module;
    }

    /// <summary>A snapshot built by hand, for the parts of the contract that need no database.</summary>
    private static TranslationSnapshot SnapshotWith(
        IReadOnlyDictionary<string, string>? entities = null,
        IReadOnlyDictionary<string, string>? wholeValues = null,
        TermMatcher? allCategories = null,
        IEnumerable<string>? disabledRoutes = null)
        => new(
            Guid.NewGuid(),
            "te",
            entities ?? new Dictionary<string, string>(StringComparer.Ordinal),
            wholeValues ?? new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, TermMatcher>(StringComparer.OrdinalIgnoreCase),
            allCategories ?? new TermMatcher(new Dictionary<string, string>(StringComparer.Ordinal)),
            (disabledRoutes ?? Array.Empty<string>())
                .Select(TranslationSnapshot.NormaliseRoute)
                .ToHashSet(StringComparer.Ordinal));

    // ------------------------------------------------------------------ the English short-circuit

    [Fact]
    public async Task English_and_missing_codes_return_null_without_opening_a_scope()
    {
        using var harness = new TestHarness();
        await AddLanguageAsync(harness, "Telugu", "te");
        var (catalog, scopes) = NewCatalog(harness);

        Assert.Null(await catalog.GetAsync("en"));
        Assert.Null(await catalog.GetAsync("EN"));   // codes are lower-invariant'd
        Assert.Null(await catalog.GetAsync("  en ")); // ...and trimmed
        Assert.Null(await catalog.GetAsync(null));
        Assert.Null(await catalog.GetAsync(string.Empty));
        Assert.Null(await catalog.GetAsync("   "));

        Assert.Equal(0, scopes.ScopesCreated);
    }

    [Fact]
    public async Task Unknown_language_code_returns_null()
    {
        using var harness = new TestHarness();
        await AddLanguageAsync(harness, "Telugu", "te");
        var (catalog, _) = NewCatalog(harness);

        Assert.Null(await catalog.GetAsync("zz"));
    }

    [Fact]
    public async Task Deactivated_language_returns_null_even_though_the_row_exists()
    {
        using var harness = new TestHarness();
        var hindi = await AddLanguageAsync(harness, "Hindi", "hi", isActive: false);
        await AddTermAsync(harness, hindi.Id, "Navami", "[navami]");
        var (catalog, _) = NewCatalog(harness);

        Assert.Null(await catalog.GetAsync("hi"));
    }

    // ------------------------------------------------------------------ what a snapshot contains

    [Fact]
    public async Task Snapshot_resolves_whole_values_and_per_row_entity_overrides()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");
        var moduleId = Guid.NewGuid().ToString();
        await AddEntityTranslationAsync(
            harness, telugu.Id, nameof(MenuModule), moduleId, nameof(MenuModule.Name), "[panchangam]");

        var (catalog, _) = NewCatalog(harness);
        var snapshot = await catalog.GetAsync("te");

        Assert.NotNull(snapshot);
        Assert.Equal("te", snapshot!.Code);
        Assert.Equal(telugu.Id, snapshot.LanguageId);
        Assert.False(snapshot.IsEmpty);

        // Whole-value keys are stored normalised, so a differently cased/spaced value still hits.
        Assert.True(snapshot.TryGetWholeValue(TermMatcher.NormaliseKey("  NAVAMI  "), out var whole),
            "A term stored as 'Navami' should be reachable by its normalised key.");
        Assert.Equal("[navami]", whole);
        Assert.False(snapshot.TryGetWholeValue("dasami", out _));

        // TranslationSnapshot.EntityKey and the key the catalog stores under must agree.
        var bundleKey = TranslationSnapshot.EntityKey(nameof(MenuModule), moduleId, nameof(MenuModule.Name));
        Assert.True(snapshot.TryGetEntity(bundleKey, out var entity),
            "The per-row override should be reachable by TranslationSnapshot.EntityKey.");
        Assert.Equal("[panchangam]", entity);
        Assert.False(snapshot.TryGetEntity(
            TranslationSnapshot.EntityKey(nameof(MenuModule), moduleId, nameof(MenuModule.Description)), out _));
    }

    [Fact]
    public async Task Untranslated_inactive_and_blank_rows_are_left_out_of_the_snapshot()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");

        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");            // the only usable one
        await AddTermAsync(harness, telugu.Id, "Dasami", null);                  // harvested, no text yet
        await AddTermAsync(harness, telugu.Id, "Tadiya", "   ");                 // cleared by an admin
        await AddTermAsync(harness, telugu.Id, "Ekadasi", "[ekadasi]", isActive: false);
        await AddEntityTranslationAsync(harness, telugu.Id, "Deity", "7", "Name", "  ");

        var (catalog, _) = NewCatalog(harness);
        var snapshot = await catalog.GetAsync("te");

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.TryGetWholeValue("navami", out _), "The translated term should be present.");
        Assert.False(snapshot.TryGetWholeValue("dasami", out _));
        Assert.False(snapshot.TryGetWholeValue("tadiya", out _));
        Assert.False(snapshot.TryGetWholeValue("ekadasi", out _));
        Assert.False(snapshot.TryGetEntity(TranslationSnapshot.EntityKey("Deity", "7", "Name"), out _));

        // Only the one usable term reaches the phrase matcher.
        Assert.Equal(1, snapshot.MatcherFor(null).TermCount);
    }

    [Fact]
    public async Task Snapshot_for_a_language_with_nothing_translated_is_empty_but_not_null()
    {
        using var harness = new TestHarness();
        await AddLanguageAsync(harness, "Sanskrit", "sa");
        var (catalog, _) = NewCatalog(harness);

        var snapshot = await catalog.GetAsync("sa");

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsEmpty,
            "An active language with no terms and no overrides has nothing to translate, so the walk must be skippable.");
    }

    [Fact]
    public async Task Matchers_are_scoped_by_category_and_fall_back_to_every_term()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]", category: "panchangam");
        await AddTermAsync(harness, telugu.Id, "Ganesha", "[ganesha]", category: "deity");
        await AddTermAsync(harness, telugu.Id, "Active", "[active]", category: "   "); // blank -> "general"

        var (catalog, _) = NewCatalog(harness);
        var snapshot = await catalog.GetAsync("te");
        Assert.NotNull(snapshot);

        var panchangam = snapshot!.MatcherFor("panchangam");
        Assert.Equal(1, panchangam.TermCount);
        Assert.Equal("[navami] today", panchangam.Translate("Navami today"));
        Assert.Equal("Ganesha today", panchangam.Translate("Ganesha today")); // other categories stay English

        // Category lookup is case-insensitive, so a caller's casing cannot silently widen the scope.
        Assert.Same(panchangam, snapshot.MatcherFor("PANCHANGAM"));

        // A blank category is bucketed as "general" rather than dropped.
        Assert.Equal(1, snapshot.MatcherFor("general").TermCount);

        // No category, or one nobody registered, means "use everything".
        var all = snapshot.MatcherFor(null);
        Assert.Equal(3, all.TermCount);
        Assert.Same(all, snapshot.MatcherFor("no-such-category"));
        Assert.Equal("[navami] and [ganesha]", all.Translate("Navami and Ganesha"));
    }

    [Fact]
    public async Task Forms_an_admin_opted_out_come_back_as_disabled_routes()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");
        await AddDisabledFormAsync(harness, telugu.Id, "/Panchangam/Daily");

        var (catalog, _) = NewCatalog(harness);
        var snapshot = await catalog.GetAsync("te");
        Assert.NotNull(snapshot);

        Assert.False(snapshot!.IsRouteTranslated("/Panchangam/Daily"));
        Assert.False(snapshot.IsRouteTranslated("panchangam/daily"));   // case-insensitive
        Assert.False(snapshot.IsRouteTranslated("/PANCHANGAM/DAILY/")); // ...and slash-insensitive
        Assert.True(snapshot.IsRouteTranslated("/deities"),
            "A form that was never opted out must still translate.");
    }

    [Fact]
    public async Task A_form_left_enabled_does_not_disable_its_route()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        var module = new MenuModule
        {
            Id = Guid.NewGuid(),
            Name = "Enabled Form",
            RoutePath = "/kept",
            DisplayOrder = 99
        };
        harness.Context.Set<MenuModule>().Add(module);
        harness.Context.Set<LanguageFormConfig>().Add(new LanguageFormConfig
        {
            Id = Guid.NewGuid(),
            LanguageId = telugu.Id,
            MenuModuleId = module.Id,
            Enabled = true
        });
        await harness.Context.SaveChangesAsync();

        var (catalog, _) = NewCatalog(harness);
        var snapshot = await catalog.GetAsync("te");

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsRouteTranslated("/kept"),
            "Only rows with Enabled = false are opt-outs; an Enabled row must not block translation.");
    }

    // ------------------------------------------------------------------ caching

    [Fact]
    public async Task Snapshot_is_cached_across_calls_and_across_code_casing()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");
        var (catalog, scopes) = NewCatalog(harness);

        var first = await catalog.GetAsync("te");
        var second = await catalog.GetAsync("te");
        var upper = await catalog.GetAsync("TE");

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Same(first, upper);
        Assert.Equal(1, scopes.ScopesCreated);
    }

    [Fact]
    public async Task Invalidate_is_what_makes_a_newly_saved_translation_visible()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");
        var (catalog, scopes) = NewCatalog(harness);

        var before = await catalog.GetAsync("te");
        Assert.NotNull(before);
        Assert.False(before!.TryGetWholeValue("dasami", out _));

        // An admin saves a new translation...
        await AddTermAsync(harness, telugu.Id, "Dasami", "[dasami]");

        // ...which the cache deliberately does not notice on its own.
        var stale = await catalog.GetAsync("te");
        Assert.Same(before, stale);
        Assert.False(stale!.TryGetWholeValue("dasami", out _));
        Assert.Equal(1, scopes.ScopesCreated);

        catalog.Invalidate();

        var rebuilt = await catalog.GetAsync("te");
        Assert.NotNull(rebuilt);
        Assert.NotSame(before, rebuilt);
        Assert.True(rebuilt!.TryGetWholeValue("dasami", out var text),
            "After Invalidate the next Get must re-read the database.");
        Assert.Equal("[dasami]", text);
        Assert.Equal(2, scopes.ScopesCreated);
    }

    [Fact]
    public async Task Invalidate_before_anything_was_cached_is_harmless()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");
        var (catalog, scopes) = NewCatalog(harness);

        catalog.Invalidate();

        var snapshot = await catalog.GetAsync("te");
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.TryGetWholeValue("navami", out _));
        Assert.Equal(1, scopes.ScopesCreated);
    }

    [Fact]
    public async Task Concurrent_first_requests_build_the_snapshot_exactly_once()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        await AddTermAsync(harness, telugu.Id, "Navami", "[navami]");
        var (catalog, scopes) = NewCatalog(harness);

        // Both callers must end up on the same build: the repositories share one DbContext, so a
        // second concurrent build would also be a concurrent DbContext use.
        using var gate = new ManualResetEventSlim(false);

        async Task<TranslationSnapshot?> Race()
        {
            gate.Wait();
            return await catalog.GetAsync("te");
        }

        var left = Task.Run(Race);
        var right = Task.Run(Race);
        gate.Set();

        var results = await Task.WhenAll(left, right);

        Assert.NotNull(results[0]);
        Assert.Same(results[0], results[1]);
        Assert.True(results[0]!.TryGetWholeValue("navami", out _), "The shared snapshot must be usable.");
        Assert.Equal(1, scopes.ScopesCreated);
    }

    [Fact]
    public async Task Different_language_codes_get_independent_snapshots()
    {
        using var harness = new TestHarness();
        var telugu = await AddLanguageAsync(harness, "Telugu", "te");
        var hindi = await AddLanguageAsync(harness, "Hindi", "hi");
        await AddTermAsync(harness, telugu.Id, "Navami", "[te-navami]");
        await AddTermAsync(harness, hindi.Id, "Dasami", "[hi-dasami]");

        var (catalog, scopes) = NewCatalog(harness);
        var te = await catalog.GetAsync("te");
        var hi = await catalog.GetAsync("hi");

        Assert.NotNull(te);
        Assert.NotNull(hi);
        Assert.NotSame(te, hi);
        Assert.Equal(hindi.Id, hi!.LanguageId);

        // "Navami" has a Telugu text and no Hindi one, and vice versa.
        Assert.True(te!.TryGetWholeValue("navami", out var teText));
        Assert.Equal("[te-navami]", teText);
        Assert.False(te.TryGetWholeValue("dasami", out _));

        Assert.True(hi.TryGetWholeValue("dasami", out var hiText));
        Assert.Equal("[hi-dasami]", hiText);
        Assert.False(hi.TryGetWholeValue("navami", out _));

        Assert.Equal(2, scopes.ScopesCreated);
    }

    // ------------------------------------------------------------------ snapshot, no database

    [Fact]
    public void IsEmpty_is_true_only_when_all_three_tiers_are_empty()
    {
        Assert.True(SnapshotWith().IsEmpty);

        Assert.False(
            SnapshotWith(entities: new Dictionary<string, string>(StringComparer.Ordinal) { ["Deity:7:Name"] = "[x]" })
                .IsEmpty,
            "A per-row override alone is still something to translate.");

        Assert.False(
            SnapshotWith(wholeValues: new Dictionary<string, string>(StringComparer.Ordinal) { ["navami"] = "[x]" })
                .IsEmpty,
            "A whole-value hit alone is still something to translate.");

        Assert.False(
            SnapshotWith(allCategories: new TermMatcher(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Navami"] = "[x]" })).IsEmpty,
            "A phrase term alone is still something to translate.");
    }

    [Fact]
    public void IsRouteTranslated_is_true_when_no_form_was_opted_out()
    {
        var snapshot = SnapshotWith();

        Assert.True(snapshot.IsRouteTranslated("/panchangam"));
        Assert.True(snapshot.IsRouteTranslated("anything at all"));
        Assert.True(snapshot.IsRouteTranslated(null));
    }

    [Fact]
    public void IsRouteTranslated_ignores_case_surrounding_slashes_and_whitespace()
    {
        var snapshot = SnapshotWith(disabledRoutes: new[] { "/Chants-Config" });

        Assert.False(snapshot.IsRouteTranslated("/Chants-Config"));
        Assert.False(snapshot.IsRouteTranslated("chants-config"));
        Assert.False(snapshot.IsRouteTranslated("/chants-config/"));
        Assert.False(snapshot.IsRouteTranslated("  //CHANTS-CONFIG//  "));

        Assert.True(snapshot.IsRouteTranslated("/chants"),
            "A prefix of a disabled route is a different form and must still translate.");
        Assert.True(snapshot.IsRouteTranslated("/chants-config/edit"));
    }

    [Fact]
    public void IsRouteTranslated_defaults_to_true_for_a_missing_route()
    {
        // A response with no route (a background job, a non-MVC path) must not be silently skipped
        // just because some other form was opted out.
        var snapshot = SnapshotWith(disabledRoutes: new[] { "/chants-config" });

        Assert.True(snapshot.IsRouteTranslated(null));
        Assert.True(snapshot.IsRouteTranslated(string.Empty));
        Assert.True(snapshot.IsRouteTranslated("   "));
    }

    [Fact]
    public void NormaliseRoute_trims_whitespace_then_slashes_and_lowercases()
    {
        Assert.Equal("chants-config", TranslationSnapshot.NormaliseRoute("/Chants-Config"));
        Assert.Equal("chants-config", TranslationSnapshot.NormaliseRoute("  /chants-config/  "));
        Assert.Equal("chants-config", TranslationSnapshot.NormaliseRoute("//CHANTS-CONFIG//"));
        Assert.Equal("chants-config", TranslationSnapshot.NormaliseRoute("chants-config"));

        // Interior slashes are structure and are kept.
        Assert.Equal("panchangam/daily", TranslationSnapshot.NormaliseRoute("/Panchangam/Daily/"));

        // The root route collapses to the empty string, which is exactly why IsRouteTranslated
        // treats a blank route as "translate" rather than looking it up.
        Assert.Equal(string.Empty, TranslationSnapshot.NormaliseRoute("/"));
        Assert.Equal(string.Empty, TranslationSnapshot.NormaliseRoute("   "));

        // Normalisation is idempotent — the write path and the read path must not drift.
        var once = TranslationSnapshot.NormaliseRoute("/Panchangam/Daily/");
        Assert.Equal(once, TranslationSnapshot.NormaliseRoute(once));
    }

    [Fact]
    public void EntityKey_matches_the_key_shape_stored_by_the_domain()
    {
        Assert.Equal(
            EntityTranslation.BundleKey(nameof(MenuModule), "7f3a", nameof(MenuModule.Name)),
            TranslationSnapshot.EntityKey(nameof(MenuModule), "7f3a", nameof(MenuModule.Name)));

        Assert.Equal("MenuModule:7f3a:Name", TranslationSnapshot.EntityKey("MenuModule", "7f3a", "Name"));
    }
}

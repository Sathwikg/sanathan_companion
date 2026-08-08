using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Sanathana.Companion.Api.Filters;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The gate-keeping behaviour of <see cref="TranslationResultFilter"/>: which responses are
/// translated at all, which language the caller is deemed to have asked for, and what happens when
/// translation goes wrong.
/// </summary>
/// <remarks>
/// <para>These are the rules that cannot be recovered from by a retry. Translating a write response
/// corrupts a round-trip; forgetting <c>Vary</c> lets a proxy hand a Telugu payload to an English
/// client; a fault in the catalog turning a 200 into a 500 takes the whole API down for the sake of
/// some text. So each one is pinned here rather than left to the integration surface.</para>
/// <para>The catalog is a fake — it records the code it was asked for, so the Accept-Language /
/// query-string / header precedence chain (private in the filter) is asserted through the only
/// public evidence of it: what the filter actually requested. The miss log is the real
/// <see cref="TranslationMissLog"/>, since "nothing was translated" and "nothing was even attempted"
/// are different states and the log is what tells them apart.</para>
/// <para>Translations are ASCII markers such as "[preserver]" rather than real Telugu, so no test can
/// fail because of how this file was encoded on disk.</para>
/// </remarks>
public class TranslationResultFilterTests
{
    private static readonly Guid RowId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LanguageId = new("22222222-2222-2222-2222-222222222222");

    /// <summary>Stands in for the controller instance MVC passes to a result filter.</summary>
    private sealed class StubController { }

    private static readonly object Controller = new StubController();

    /// <summary>
    /// A catalog whose answer the test dictates. Records every code asked for and the token handed
    /// over, which is how the language-resolution chain is observed from outside the filter.
    /// </summary>
    private sealed class FakeCatalog : ITranslationCatalog
    {
        private readonly Func<string?, TranslationSnapshot?> _resolve;

        public FakeCatalog(Func<string?, TranslationSnapshot?> resolve) => _resolve = resolve;

        public List<string?> RequestedCodes { get; } = [];

        public CancellationToken LastToken { get; private set; }

        public Task<TranslationSnapshot?> GetAsync(string? languageCode, CancellationToken cancellationToken = default)
        {
            RequestedCodes.Add(languageCode);
            LastToken = cancellationToken;
            return Task.FromResult(_resolve(languageCode));
        }

        public void Invalidate() { }
    }

    // The response DTOs are public because the walker reaches their properties through compiled
    // accessors; keeping them visible removes any question of reflection visibility from the tests.

    /// <summary>One row, carrying each translatable shape the filter has to reach.</summary>
    public sealed class DeityRowDto
    {
        public Guid Id { get; set; }

        /// <summary>Per-row override first, dictionary second.</summary>
        [Translatable("Deity", nameof(Id))]
        public string Name { get; set; } = string.Empty;

        /// <summary>Controlled vocabulary: whole-value lookup only.</summary>
        [Translatable]
        public string Classification { get; set; } = string.Empty;

        /// <summary>Composite: phrase substitution inside a value that embeds times.</summary>
        [Translatable(Composite = true)]
        public string Tithi { get; set; } = string.Empty;

        /// <summary>Unmarked, so translation must never touch it whatever the dictionary says.</summary>
        public string DevoteeNote { get; set; } = string.Empty;
    }

    /// <summary>A wrapper, so the walk has to descend through a collection to do anything at all.</summary>
    public sealed class DeityListDto
    {
        public string Title { get; set; } = string.Empty;

        public List<DeityRowDto> Items { get; set; } = [];
    }

    // ---------------------------------------------------------------- builders

    private static TranslationSnapshot Snapshot(
        string code = "te",
        (string Key, string Text)[]? entities = null,
        (string English, string Translated)[]? wholeValues = null,
        (string Term, string Translated)[]? terms = null,
        string[]? disabledRoutes = null)
    {
        var entityMap = (entities ?? Array.Empty<(string Key, string Text)>())
            .ToDictionary(e => e.Key, e => e.Text, StringComparer.Ordinal);

        var wholeMap = (wholeValues ?? Array.Empty<(string English, string Translated)>())
            .ToDictionary(v => TermMatcher.NormaliseKey(v.English), v => v.Translated, StringComparer.Ordinal);

        var matcher = new TermMatcher(
            (terms ?? Array.Empty<(string Term, string Translated)>())
                .ToDictionary(t => t.Term, t => t.Translated, StringComparer.Ordinal));

        var disabled = (disabledRoutes ?? Array.Empty<string>())
            .Select(TranslationSnapshot.NormaliseRoute)
            .ToHashSet(StringComparer.Ordinal);

        return new TranslationSnapshot(
            LanguageId, code, entityMap, wholeMap,
            new Dictionary<string, TermMatcher>(StringComparer.Ordinal), matcher, disabled);
    }

    /// <summary>A catalog that knows exactly one language; every other code (including "en") is null.</summary>
    private static FakeCatalog Catalog(TranslationSnapshot? snapshot, string forCode = "te")
        => new(code => string.Equals(code, forCode, StringComparison.OrdinalIgnoreCase) ? snapshot : null);

    /// <summary>The vocabulary most tests translate against.</summary>
    private static TranslationSnapshot TeluguSnapshot(string[]? disabledRoutes = null) => Snapshot(
        entities: [(TranslationSnapshot.EntityKey("Deity", RowId.ToString(), "Name"), "[vishnu-row]")],
        wholeValues: [("Preserver", "[preserver]")],
        terms: [("Navami", "[navami]"), ("upto", "[upto]")],
        disabledRoutes: disabledRoutes);

    private static DeityRowDto Row() => new()
    {
        Id = RowId,
        Name = "Vishnu",
        Classification = "Preserver",
        Tithi = "Navami upto 16:37",
        DevoteeNote = "Preserver"   // same text as a translatable value, but unmarked
    };

    private static TranslationResultFilter Filter(ITranslationCatalog catalog, ITranslationMissLog misses)
        => new(catalog, misses, NullLogger<TranslationResultFilter>.Instance);

    private static ResultExecutingContext Context(
        object? payload,
        string method = "GET",
        string? language = null,
        string? acceptLanguage = null,
        string? queryString = null,
        string? route = null,
        string? optOut = null,
        CancellationToken abort = default)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = "/api/deities";
        http.RequestAborted = abort;

        if (language is not null) http.Request.Headers[TranslationResultFilter.LanguageHeader] = language;
        if (route is not null) http.Request.Headers[TranslationResultFilter.RouteHeader] = route;
        if (optOut is not null) http.Request.Headers[TranslationResultFilter.OptOutHeader] = optOut;
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        if (queryString is not null) http.Request.QueryString = new QueryString(queryString);

        IActionResult result = payload as IActionResult ?? new ObjectResult(payload);

        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ResultExecutingContext(action, new List<IFilterMetadata>(), result, Controller);
    }

    /// <summary>Runs the filter and reports whether the rest of the pipeline was invoked.</summary>
    private static async Task<bool> RunAsync(TranslationResultFilter filter, ResultExecutingContext context)
    {
        var nextCalled = false;

        await filter.OnResultExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(
                new ResultExecutedContext(context, context.Filters, context.Result, Controller));
        });

        return nextCalled;
    }

    private static string VaryOf(ResultExecutingContext context)
        => context.HttpContext.Response.Headers.Vary.ToString();

    private static void AssertUntranslated(DeityRowDto row)
    {
        Assert.Equal("Vishnu", row.Name);
        Assert.Equal("Preserver", row.Classification);
        Assert.Equal("Navami upto 16:37", row.Tithi);
    }

    // ---------------------------------------------------------------- cache correctness

    /// <summary>
    /// Vary is the one header that has to be right on responses the filter deliberately does NOT
    /// translate: those are exactly the payloads a shared cache could otherwise replay to a client
    /// of a different language.
    /// </summary>
    [Fact]
    public async Task Vary_is_advertised_on_every_response_including_untranslated_ones()
    {
        (string Method, string? Language, string? OptOut)[] cases =
        [
            ("GET", "te", null),     // translated
            ("GET", "en", null),     // English: the catalog has no snapshot
            ("GET", null, null),     // no language asked for at all
            ("POST", "te", null),    // a write
            ("GET", "te", "none")    // the round-trip guard
        ];

        foreach (var (method, language, optOut) in cases)
        {
            var context = Context(Row(), method: method, language: language, optOut: optOut);
            var nextCalled = await RunAsync(
                Filter(Catalog(TeluguSnapshot()), new TranslationMissLog()), context);

            Assert.True(nextCalled, $"next() must run for {method} language={language ?? "<none>"}.");
            Assert.Contains(
                TranslationResultFilter.LanguageHeader,
                VaryOf(context),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- when translation is skipped

    /// <summary>A write echoes back what the client sent; translating it would corrupt the save.</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task A_write_response_is_never_translated(string method)
    {
        var row = Row();
        var catalog = Catalog(TeluguSnapshot());
        var misses = new TranslationMissLog();

        await RunAsync(Filter(catalog, misses), Context(row, method: method, language: "te"));

        AssertUntranslated(row);
        Assert.Empty(catalog.RequestedCodes);   // not even asked: the method check comes first
        Assert.Equal(0, misses.Count);
    }

    /// <summary>The edit-screen opt-out: load in English so the save writes English back.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    [InlineData("None")]
    public async Task The_opt_out_header_suppresses_translation_even_with_a_valid_language(string optOut)
    {
        var row = Row();
        var catalog = Catalog(TeluguSnapshot());
        var misses = new TranslationMissLog();

        await RunAsync(Filter(catalog, misses), Context(row, language: "te", optOut: optOut));

        AssertUntranslated(row);
        Assert.Empty(catalog.RequestedCodes);
        Assert.Equal(0, misses.Count);
    }

    /// <summary>Only "none" opts out — any other value must not silently disable the feature.</summary>
    [Fact]
    public async Task An_unrecognised_opt_out_value_does_not_suppress_translation()
    {
        var row = Row();

        await RunAsync(
            Filter(Catalog(TeluguSnapshot()), new TranslationMissLog()),
            Context(row, language: "te", optOut: "all"));

        Assert.Equal("[vishnu-row]", row.Name);
    }

    [Fact]
    public async Task No_language_anywhere_leaves_the_payload_untranslated()
    {
        var row = Row();
        var catalog = Catalog(TeluguSnapshot());
        var misses = new TranslationMissLog();

        await RunAsync(Filter(catalog, misses), Context(row));

        AssertUntranslated(row);
        Assert.Empty(catalog.RequestedCodes);   // no code resolved, so no lookup at all
        Assert.Equal(0, misses.Count);
    }

    /// <summary>English (and any unknown code) has no snapshot, so the payload stays as it is.</summary>
    [Fact]
    public async Task A_language_the_catalog_does_not_know_leaves_the_payload_untranslated()
    {
        var row = Row();
        var catalog = Catalog(TeluguSnapshot());

        await RunAsync(Filter(catalog, new TranslationMissLog()), Context(row, language: "en"));

        AssertUntranslated(row);
        Assert.Equal("en", Assert.Single(catalog.RequestedCodes));   // asked, and answered with nothing
    }

    /// <summary>A language with an empty dictionary skips the walk rather than walking for nothing.</summary>
    [Fact]
    public async Task An_empty_snapshot_leaves_the_payload_untranslated()
    {
        var row = Row();
        var misses = new TranslationMissLog();
        var empty = Snapshot();

        Assert.True(empty.IsEmpty, "The fixture is only meaningful if the snapshot really is empty.");

        await RunAsync(Filter(Catalog(empty), misses), Context(row, language: "te"));

        AssertUntranslated(row);
        Assert.Equal(0, misses.Count);   // nothing was attempted, so nothing counts as a miss
    }

    /// <summary>A result with no object to walk must pass straight through, not throw.</summary>
    [Fact]
    public async Task A_result_with_no_payload_is_left_alone()
    {
        foreach (object? payload in new object?[] { new NoContentResult(), null })
        {
            var catalog = Catalog(TeluguSnapshot());
            var context = Context(payload, language: "te");

            var nextCalled = await RunAsync(Filter(catalog, new TranslationMissLog()), context);

            Assert.True(nextCalled, "next() must run even when there is nothing to translate.");
            Assert.Empty(catalog.RequestedCodes);
            Assert.Contains(TranslationResultFilter.LanguageHeader, VaryOf(context), StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- language resolution

    /// <summary>"te-IN,te;q=0.9,en;q=0.8" is a browser preference list, not a language code.</summary>
    [Fact]
    public async Task Accept_language_is_reduced_to_its_first_base_code()
    {
        var row = Row();
        var catalog = Catalog(TeluguSnapshot());

        await RunAsync(
            Filter(catalog, new TranslationMissLog()),
            Context(row, acceptLanguage: "te-IN,te;q=0.9,en;q=0.8"));

        Assert.Equal("te", Assert.Single(catalog.RequestedCodes));
        Assert.Equal("[vishnu-row]", row.Name);
    }

    [Theory]
    [InlineData("te", "te")]
    [InlineData("te-IN", "te")]
    [InlineData("en-GB,en;q=0.9", "en")]
    [InlineData("  te-IN  ,en", "te")]
    public async Task Accept_language_parsing_covers_the_shapes_browsers_send(string accept, string expected)
    {
        var catalog = Catalog(TeluguSnapshot());

        await RunAsync(Filter(catalog, new TranslationMissLog()), Context(Row(), acceptLanguage: accept));

        Assert.Equal(expected, Assert.Single(catalog.RequestedCodes));
    }

    /// <summary>?lang= exists for links and embeds that cannot set a header; it beats Accept-Language.</summary>
    [Fact]
    public async Task The_lang_query_string_is_honoured_and_outranks_accept_language()
    {
        var row = Row();
        var catalog = Catalog(TeluguSnapshot());

        await RunAsync(
            Filter(catalog, new TranslationMissLog()),
            Context(row, acceptLanguage: "en-GB,en;q=0.9", queryString: "?lang=te"));

        Assert.Equal("te", Assert.Single(catalog.RequestedCodes));
        Assert.Equal("[vishnu-row]", row.Name);
    }

    /// <summary>The app's own choice beats the browser's: a Telugu user on an English laptop.</summary>
    [Fact]
    public async Task The_explicit_language_header_outranks_the_query_string_and_accept_language()
    {
        var catalog = Catalog(TeluguSnapshot());

        await RunAsync(
            Filter(catalog, new TranslationMissLog()),
            Context(Row(), language: "te", queryString: "?lang=hi", acceptLanguage: "en-GB,en;q=0.8"));

        Assert.Equal("te", Assert.Single(catalog.RequestedCodes));
    }

    [Fact]
    public async Task A_padded_language_header_is_trimmed_and_a_blank_one_falls_through()
    {
        var padded = Catalog(TeluguSnapshot());
        await RunAsync(Filter(padded, new TranslationMissLog()), Context(Row(), language: "  te  "));
        Assert.Equal("te", Assert.Single(padded.RequestedCodes));

        // Whitespace is not a choice, so the next source in the chain gets its turn.
        var blank = Catalog(TeluguSnapshot());
        await RunAsync(
            Filter(blank, new TranslationMissLog()),
            Context(Row(), language: "   ", queryString: "?lang=te"));
        Assert.Equal("te", Assert.Single(blank.RequestedCodes));
    }

    // ---------------------------------------------------------------- per-form opt-out

    /// <summary>An admin can switch a whole form back to English on the Forms tab.</summary>
    [Fact]
    public async Task A_route_disabled_for_the_language_leaves_the_payload_untranslated()
    {
        var snapshot = TeluguSnapshot(disabledRoutes: ["panchangam"]);

        var blocked = Row();
        var misses = new TranslationMissLog();
        await RunAsync(
            Filter(Catalog(snapshot), misses),
            Context(blocked, language: "te", route: "/panchangam"));

        AssertUntranslated(blocked);
        Assert.Equal(0, misses.Count);   // the form was skipped, not tried and failed

        // A route that was not opted out still translates, so this is a per-route rule.
        var allowed = Row();
        await RunAsync(
            Filter(Catalog(snapshot), new TranslationMissLog()),
            Context(allowed, language: "te", route: "/deities"));

        Assert.Equal("[vishnu-row]", allowed.Name);
    }

    /// <summary>The client sends whatever its router holds; matching must survive case and slashes.</summary>
    [Theory]
    [InlineData("/panchangam")]
    [InlineData("panchangam")]
    [InlineData("/Panchangam/")]
    [InlineData("  PANCHANGAM  ")]
    public async Task Route_matching_ignores_case_and_surrounding_slashes(string route)
    {
        var row = Row();

        await RunAsync(
            Filter(Catalog(TeluguSnapshot(disabledRoutes: ["/Panchangam/"])), new TranslationMissLog()),
            Context(row, language: "te", route: route));

        AssertUntranslated(row);
    }

    /// <summary>No route header (a non-UI caller) means no opt-out can apply.</summary>
    [Fact]
    public async Task A_response_with_no_route_header_is_still_translated()
    {
        var row = Row();

        await RunAsync(
            Filter(Catalog(TeluguSnapshot(disabledRoutes: ["panchangam"])), new TranslationMissLog()),
            Context(row, language: "te"));

        Assert.Equal("[vishnu-row]", row.Name);
    }

    // ---------------------------------------------------------------- the happy path

    /// <summary>
    /// The whole point of the filter: for a valid language the DTO really is rewritten in place,
    /// through each tier of the resolution chain, and unmarked text is left exactly as it was.
    /// </summary>
    [Fact]
    public async Task A_valid_language_rewrites_the_payload_through_every_tier()
    {
        var row = Row();
        var context = Context(row, language: "te");

        var nextCalled = await RunAsync(
            Filter(Catalog(TeluguSnapshot()), new TranslationMissLog()), context);

        Assert.True(nextCalled, "next() must still run after a successful translation.");
        Assert.Equal("[vishnu-row]", row.Name);                 // per-row override
        Assert.Equal("[preserver]", row.Classification);        // whole-value dictionary hit
        Assert.Equal("[navami] [upto] 16:37", row.Tithi);       // phrase substitution, time intact
        Assert.Equal("Preserver", row.DevoteeNote);             // unmarked: translation is opt-in

        // The result object itself is mutated in place rather than replaced.
        Assert.Same(row, Assert.IsType<ObjectResult>(context.Result).Value);
    }

    /// <summary>The walk has to reach rows inside collections, which is how every list endpoint looks.</summary>
    [Fact]
    public async Task Rows_nested_inside_a_collection_are_translated_too()
    {
        var payload = new DeityListDto
        {
            Title = "Preserver",   // unmarked wrapper text must survive untouched
            Items = [Row(), new DeityRowDto { Id = Guid.Empty, Name = "Vishnu", Classification = "Preserver" }]
        };

        await RunAsync(Filter(Catalog(TeluguSnapshot()), new TranslationMissLog()), Context(payload, language: "te"));

        Assert.Equal("Preserver", payload.Title);
        Assert.Equal("[vishnu-row]", payload.Items[0].Name);
        Assert.Equal("[preserver]", payload.Items[1].Classification);
        // The second row has no per-row override of its own and "Vishnu" is not in the dictionary.
        Assert.Equal("Vishnu", payload.Items[1].Name);
    }

    /// <summary>
    /// What the app tried to show and could not translate has to reach the admin's worklist —
    /// otherwise the dictionary can only ever cover text that already sits in a scannable column.
    /// </summary>
    [Fact]
    public async Task Values_that_could_not_be_translated_are_recorded_as_misses()
    {
        var row = new DeityRowDto
        {
            Id = Guid.Empty,          // no per-row override for this key
            Name = "Ganesha",         // not in the dictionary
            Classification = "Preserver",
            Tithi = string.Empty      // blank: nothing was attempted, so not a miss
        };

        var misses = new TranslationMissLog();
        await RunAsync(Filter(Catalog(TeluguSnapshot()), misses), Context(row, language: "te"));

        Assert.Equal("[preserver]", row.Classification);

        var drained = misses.Drain();
        Assert.Equal("Ganesha", Assert.Single(drained).Value);
    }

    // ---------------------------------------------------------------- resilience

    /// <summary>
    /// Translation is a nicety; the response is not. A catalog fault must cost the caller their
    /// translated text and nothing else — the pipeline still runs and the payload still ships.
    /// </summary>
    [Fact]
    public async Task A_catalog_failure_still_calls_next_and_leaves_the_response_intact()
    {
        var row = Row();
        var exploding = new FakeCatalog(_ => throw new InvalidOperationException("catalog is down"));
        var context = Context(row, language: "te");

        var nextCalled = await RunAsync(Filter(exploding, new TranslationMissLog()), context);

        Assert.True(nextCalled, "A translation fault must not stop the response from being written.");
        AssertUntranslated(row);
        Assert.Same(row, Assert.IsType<ObjectResult>(context.Result).Value);
        Assert.Contains(TranslationResultFilter.LanguageHeader, VaryOf(context), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A client that has gone away must be able to cancel the snapshot build with it.</summary>
    [Fact]
    public async Task The_request_abort_token_is_handed_to_the_catalog()
    {
        using var cts = new CancellationTokenSource();
        var catalog = Catalog(TeluguSnapshot());

        await RunAsync(
            Filter(catalog, new TranslationMissLog()),
            Context(Row(), language: "te", abort: cts.Token));

        Assert.Equal(cts.Token, catalog.LastToken);
    }
}

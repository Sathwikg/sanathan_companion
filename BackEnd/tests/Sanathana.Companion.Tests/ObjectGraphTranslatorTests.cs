using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Behavioural contract for <see cref="ObjectGraphTranslator"/> — the walker that rewrites the
/// <see cref="TranslatableAttribute"/> strings of a response on the way out of the API.
/// </summary>
/// <remarks>
/// <para>Two things are protected here. The first is the <b>resolution chain</b>: a per-row
/// EntityTranslation override beats a whole-value dictionary hit, which beats phrase substitution,
/// which beats leaving the English alone. Getting that order wrong is silent — the response still
/// looks translated, it is just translated with the wrong text — so it is pinned explicitly.</para>
/// <para>The second is the <b>opt-in guard</b>. Translation applies only to properties carrying the
/// attribute, which is the entire reason personal data (names, e-mail, feedback text) never enters
/// the pipeline. Several tests deliberately put a value that IS in the dictionary on a property that
/// is NOT annotated and assert it comes back byte-identical; those tests fail the moment the walker
/// starts translating anything it merely happens to reach.</para>
/// <para><c>TypeMapCache</c> is <c>internal</c> to the Application assembly and the test
/// project has no <c>InternalsVisibleTo</c>, so it is exercised through the walker — its inertness
/// pruning, <c>[NoTranslate]</c> handling, child discovery and unsupported-shape rules are all
/// observable as "was this property rewritten or not".</para>
/// <para>Translations are ASCII markers such as "[navami]" rather than real Telugu, matching
/// <see cref="TermMatcherTests"/>: no test can then fail because of how the file was encoded.</para>
/// </remarks>
public class ObjectGraphTranslatorTests
{
    private static readonly Guid LanguageId = new("99999999-9999-9999-9999-999999999999");
    private static readonly Guid RowA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RowB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RowC = new("33333333-3333-3333-3333-333333333333");

    /// <summary>Controlled vocabulary used both as whole values and as phrase terms.</summary>
    private static readonly Dictionary<string, string> Vocabulary = new(StringComparer.Ordinal)
    {
        ["Navami"] = "[navami]",
        ["Dasami"] = "[dasami]",
        ["Krishna"] = "[krishna]",
        ["upto"] = "[upto]",
        ["from"] = "[from]"
    };

    /// <summary>
    /// Builds a snapshot the way <c>TranslationCatalog</c> does: whole-value keys are normalised
    /// through <see cref="TermMatcher.NormaliseKey"/>, entity keys are the raw bundle keys.
    /// </summary>
    private static TranslationSnapshot Snapshot(
        Dictionary<string, string>? entities = null,
        Dictionary<string, string>? wholeValues = null,
        Dictionary<string, string>? terms = null,
        Dictionary<string, Dictionary<string, string>>? termsByCategory = null)
    {
        var matchers = (termsByCategory ?? new Dictionary<string, Dictionary<string, string>>())
            .ToDictionary(kv => kv.Key, kv => new TermMatcher(kv.Value), StringComparer.OrdinalIgnoreCase);

        var whole = (wholeValues ?? new Dictionary<string, string>())
            .ToDictionary(kv => TermMatcher.NormaliseKey(kv.Key), kv => kv.Value, StringComparer.Ordinal);

        return new TranslationSnapshot(
            LanguageId,
            "te",
            entities ?? new Dictionary<string, string>(StringComparer.Ordinal),
            whole,
            matchers,
            new TermMatcher(terms ?? new Dictionary<string, string>(StringComparer.Ordinal)),
            new HashSet<string>(StringComparer.Ordinal));
    }

    private static string EntityKey(Guid id, string field) => TranslationSnapshot.EntityKey("Deity", id.ToString(), field);

    /// <summary>
    /// Counts every <see cref="ITranslationMissLog.Record"/> call rather than de-duplicating them as
    /// the production log does — the raw call count is how the per-response memo is observed.
    /// </summary>
    private sealed class RecordingMissLog : ITranslationMissLog
    {
        private readonly List<TranslationMiss> _recorded = [];

        public IReadOnlyList<TranslationMiss> Recorded => _recorded;

        public int Calls => _recorded.Count;

        public int Count => _recorded.Count;

        public void Record(string value, string? category) => _recorded.Add(new TranslationMiss(value, category, 1));

        public IReadOnlyList<TranslationMiss> Drain()
        {
            var copy = _recorded.ToList();
            _recorded.Clear();
            return copy;
        }
    }

    // ------------------------------------------------------------------ resolution order

    private sealed class DeityRow
    {
        public Guid Id { get; set; }

        [Translatable("Deity", nameof(Id), Composite = true)]
        public string? Name { get; set; }
    }

    [Fact]
    public void Resolution_order_prefers_row_override_then_dictionary_then_phrases_then_english()
    {
        // Every tier is configured at once, so each row can only land on the right answer if the
        // chain is ordered correctly: "Krishna" is reachable as a row override AND as a whole value
        // AND as a phrase term.
        var snapshot = Snapshot(
            entities: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EntityKey(RowA, nameof(DeityRow.Name))] = "[row-override]"
            },
            wholeValues: Vocabulary,
            terms: Vocabulary);

        var rows = new List<DeityRow>
        {
            new() { Id = RowA, Name = "Krishna" },                 // 1. per-row override
            new() { Id = RowB, Name = "Krishna" },                 // 2. whole-value dictionary
            new() { Id = RowC, Name = "Navami upto 16:37" },       // 3. phrase substitution
            new() { Id = RowC, Name = "Sthira Yoga" }              // 4. nothing matches
        };

        new ObjectGraphTranslator(snapshot).Walk(rows);

        Assert.Equal("[row-override]", rows[0].Name);
        Assert.Equal("[krishna]", rows[1].Name);
        Assert.Equal("[navami] [upto] 16:37", rows[2].Name);
        Assert.Equal("Sthira Yoga", rows[3].Name);
    }

    [Fact]
    public void Two_rows_sharing_english_text_get_their_own_overrides()
    {
        // The row override deliberately sits in front of the memo: two deities can both be called
        // "Krishna" in English and still need different Telugu. Memoising the override would give
        // the second row the first row's text.
        var snapshot = Snapshot(
            entities: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EntityKey(RowA, nameof(DeityRow.Name))] = "[krishna-of-row-a]",
                [EntityKey(RowB, nameof(DeityRow.Name))] = "[krishna-of-row-b]"
            },
            wholeValues: Vocabulary);

        var rows = new List<DeityRow>
        {
            new() { Id = RowA, Name = "Krishna" },
            new() { Id = RowB, Name = "Krishna" },
            new() { Id = RowC, Name = "Krishna" } // no override: falls through to the dictionary
        };

        new ObjectGraphTranslator(snapshot).Walk(rows);

        Assert.Equal("[krishna-of-row-a]", rows[0].Name);
        Assert.Equal("[krishna-of-row-b]", rows[1].Name);
        Assert.Equal("[krishna]", rows[2].Name);
    }

    private sealed class FieldedRow
    {
        public Guid Id { get; set; }

        [Translatable("Deity", nameof(Id))]
        public string? Name { get; set; }

        [Translatable("Deity", nameof(Id), Field = "Summary")]
        public string? Description { get; set; }
    }

    [Fact]
    public void Entity_override_uses_the_sibling_key_and_an_explicit_field_name()
    {
        var snapshot = Snapshot(entities: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EntityKey(RowA, "Name")] = "[a-name]",
            [EntityKey(RowA, "Summary")] = "[a-summary]",
            // Keyed by the PROPERTY name rather than the declared Field: must never be picked up.
            [EntityKey(RowA, "Description")] = "[wrong-field]",
            [EntityKey(RowB, "Name")] = "[b-name]"
        });

        var row = new FieldedRow { Id = RowA, Name = "Krishna", Description = "Eighth avatar" };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal("[a-name]", row.Name);                 // Field defaults to the property name
        Assert.Equal("[a-summary]", row.Description);       // explicit Field = "Summary" wins
    }

    private sealed class UnkeyedRow
    {
        public string Key { get; set; } = string.Empty;

        [Translatable("Deity", "ThisPropertyDoesNotExist")]
        public string? Missing { get; set; }

        [Translatable("Deity", nameof(Key))]
        public string? Blank { get; set; }
    }

    [Fact]
    public void Entity_override_is_skipped_when_the_key_is_missing_or_blank()
    {
        var snapshot = Snapshot(
            entities: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The key a naive implementation would build from an empty key property.
                ["Deity::Blank"] = "[should-never-apply]"
            },
            wholeValues: Vocabulary);

        var row = new UnkeyedRow { Key = "   ", Missing = "Navami", Blank = "Dasami" };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal("[navami]", row.Missing);  // KeyProperty names nothing -> straight to dictionary
        Assert.Equal("[dasami]", row.Blank);    // key is whitespace -> no bogus lookup
    }

    // ------------------------------------------------------------------ dictionary vs phrases

    private sealed class TithiRow
    {
        [Translatable(Composite = true)]
        public string? Tithi { get; set; }

        [Translatable]
        public string? Nakshatram { get; set; }
    }

    [Fact]
    public void Attribute_without_an_entity_type_does_a_dictionary_lookup_only()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary, terms: Vocabulary);

        // Padding and casing must not matter: the lookup key is normalised on both sides.
        var row = new TithiRow { Nakshatram = "  NAVAMI  " };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal("[navami]", row.Nakshatram);
    }

    [Fact]
    public void Composite_enables_phrase_substitution_and_non_composite_refuses_it()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary, terms: Vocabulary);

        // Different strings on purpose: the per-response memo is keyed on the raw value, so reusing
        // one string across both properties would prove nothing about Composite.
        var row = new TithiRow
        {
            Tithi = "Navami upto 16:37",
            Nakshatram = "Dasami from 18:20"
        };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal("[navami] [upto] 16:37", row.Tithi);
        Assert.Equal("Dasami from 18:20", row.Nakshatram); // no whole-value hit, and no substitution
    }

    private sealed class CategorisedRow
    {
        [Translatable(Composite = true, Category = "panchangam")]
        public string? Restricted { get; set; }

        [Translatable(Composite = true)]
        public string? Unrestricted { get; set; }
    }

    [Fact]
    public void Category_selects_the_matcher_used_for_phrase_substitution()
    {
        var snapshot = Snapshot(
            terms: new Dictionary<string, string>(StringComparer.Ordinal) { ["Dasami"] = "[all-dasami]" },
            termsByCategory: new Dictionary<string, Dictionary<string, string>>
            {
                ["panchangam"] = new(StringComparer.Ordinal) { ["Navami"] = "[panchangam-navami]" }
            });

        var row = new CategorisedRow
        {
            Restricted = "Navami and Dasami",
            Unrestricted = "Dasami and Navami"
        };

        new ObjectGraphTranslator(snapshot).Walk(row);

        // The panchangam matcher knows Navami but not Dasami...
        Assert.Equal("[panchangam-navami] and Dasami", row.Restricted);
        // ...and the uncategorised property falls back to the all-categories matcher, which is the
        // other way round.
        Assert.Equal("[all-dasami] and Navami", row.Unrestricted);
    }

    // ------------------------------------------------------------------ the opt-in guard

    private sealed class ProfileRow
    {
        [Translatable]
        public string? Title { get; set; }

        public string? DisplayName { get; set; }

        public string? Email { get; set; }

        public List<string> Notes { get; set; } = [];
    }

    [Fact]
    public void Properties_without_the_attribute_are_never_touched()
    {
        // Every unannotated value below IS in the dictionary. If the walker ever stopped honouring
        // the opt-in, this test is what catches it — and what it is guarding is personal data.
        var snapshot = Snapshot(wholeValues: Vocabulary);

        var row = new ProfileRow
        {
            Title = "Navami",
            DisplayName = "Krishna",
            Email = "Dasami",
            Notes = ["Krishna", "Navami"]
        };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal("[navami]", row.Title);
        Assert.Equal("Krishna", row.DisplayName);
        Assert.Equal("Dasami", row.Email);
        Assert.Equal(new[] { "Krishna", "Navami" }, row.Notes);
    }

    [NoTranslate]
    private sealed class PersonalRow
    {
        [Translatable]
        public string? Note { get; set; }
    }

    private sealed class Envelope
    {
        [Translatable]
        public string? Title { get; set; }

        [NoTranslate]
        [Translatable]
        public string? Secret { get; set; }

        public PersonalRow? Personal { get; set; }
    }

    [Fact]
    public void NoTranslate_stops_a_whole_type_and_a_single_property_being_walked()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary);

        var envelope = new Envelope
        {
            Title = "Navami",
            Secret = "Dasami",
            Personal = new PersonalRow { Note = "Krishna" }
        };

        new ObjectGraphTranslator(snapshot).Walk(envelope);

        Assert.Equal("[navami]", envelope.Title);
        Assert.Equal("Dasami", envelope.Secret);            // [NoTranslate] beats [Translatable]
        Assert.Equal("Krishna", envelope.Personal!.Note);   // the nested type is never descended into

        // The same type reached directly, not as a child, is still refused.
        var direct = new PersonalRow { Note = "Krishna" };
        new ObjectGraphTranslator(snapshot).Walk(direct);
        Assert.Equal("Krishna", direct.Note);
    }

    // ------------------------------------------------------------------ shapes the walker handles

    private sealed class TagRow
    {
        [Translatable]
        public List<string> Tags { get; set; } = [];

        [Translatable]
        public List<string> EmptyTags { get; set; } = [];
    }

    [Fact]
    public void String_lists_are_translated_element_by_element()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary);

        var row = new TagRow { Tags = ["Navami", "Sthira Yoga", "   ", "", "Krishna"] };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal(new[] { "[navami]", "Sthira Yoga", "   ", "", "[krishna]" }, row.Tags);
        Assert.Empty(row.EmptyTags);
    }

    private sealed class Child
    {
        [Translatable]
        public string? Caption { get; set; }
    }

    private sealed class Parent
    {
        [Translatable]
        public string? Title { get; set; }

        public Child? Only { get; set; }

        public List<Child> Items { get; set; } = [];

        public Dictionary<string, Child> Keyed { get; set; } = new();

        public Child[] Extras { get; set; } = [];
    }

    [Fact]
    public void Nested_objects_lists_dictionaries_and_arrays_are_all_walked()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary);

        // Distinct values throughout, so the memo cannot mask a branch the walker never reached.
        var parent = new Parent
        {
            Title = "Navami",
            Only = new Child { Caption = "Dasami" },
            Items = [new Child { Caption = "Krishna" }],
            Keyed = new Dictionary<string, Child> { ["k"] = new() { Caption = "upto" } },
            Extras = [new Child { Caption = "from" }]
        };

        new ObjectGraphTranslator(snapshot).Walk(parent);

        Assert.Equal("[navami]", parent.Title);
        Assert.Equal("[dasami]", parent.Only!.Caption);
        Assert.Equal("[krishna]", parent.Items[0].Caption);
        Assert.Equal("[upto]", parent.Keyed["k"].Caption);
        Assert.Equal("[from]", parent.Extras[0].Caption);
    }

    private sealed class Node
    {
        [Translatable]
        public string? Label { get; set; }

        public Node? Next { get; set; }
    }

    [Fact]
    public void Walking_stops_below_the_maximum_depth()
    {
        // The root is depth 0 and the bound is 8, so nodes 0..8 are rewritten and node 9 onwards is
        // left in English. A response deeper than that is a bug in the DTO, not something to chase.
        var labels = Enumerable.Range(0, 11).Select(i => $"n{i}").ToArray();

        var snapshot = Snapshot(
            wholeValues: labels.ToDictionary(l => l, l => $"[{l}]", StringComparer.Ordinal));

        var nodes = labels.Select(l => new Node { Label = l }).ToArray();
        for (var i = 0; i < nodes.Length - 1; i++) nodes[i].Next = nodes[i + 1];

        new ObjectGraphTranslator(snapshot).Walk(nodes[0]);

        for (var i = 0; i <= 8; i++)
            Assert.True(nodes[i].Label == $"[n{i}]",
                $"Node at depth {i} is within the depth bound and should have been translated, but was '{nodes[i].Label}'.");

        Assert.Equal("n9", nodes[9].Label);
        Assert.Equal("n10", nodes[10].Label);
    }

    [Fact]
    public void A_reference_cycle_does_not_hang_or_overflow_the_stack()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary);

        var first = new Node { Label = "Navami" };
        var second = new Node { Label = "Dasami" };
        first.Next = second;
        second.Next = first; // A -> B -> A

        // Reaching the assertions at all is half the point: without the reference-identity guard
        // this walk never returns.
        new ObjectGraphTranslator(snapshot).Walk(first);

        Assert.Equal("[navami]", first.Label);
        Assert.Equal("[dasami]", second.Label);
    }

    private sealed class OddShapes
    {
        [Translatable]
        public int Number { get; set; }

        [Translatable]
        public string Computed => "Navami";

        [Translatable]
        public string? Name { get; set; }
    }

    [Fact]
    public void Attributes_on_unsupported_property_shapes_are_ignored()
    {
        // A non-string, and a string with no setter: the type map skips both rather than guessing,
        // and the usable sibling on the same type still translates.
        var snapshot = Snapshot(wholeValues: Vocabulary);

        var row = new OddShapes { Number = 9, Name = "Dasami" };

        new ObjectGraphTranslator(snapshot).Walk(row);

        Assert.Equal(9, row.Number);
        Assert.Equal("Navami", row.Computed);
        Assert.Equal("[dasami]", row.Name);
    }

    private sealed class InertRow
    {
        public string? Label { get; set; }

        public int Count { get; set; }

        public List<string> Codes { get; set; } = [];

        public InertChild? Child { get; set; }
    }

    private sealed class InertChild
    {
        public string? Caption { get; set; }
    }

    [Fact]
    public void An_inert_type_is_skipped_entirely()
    {
        // Nothing anywhere under InertRow carries the attribute, so the whole graph is pruned. Every
        // value here would translate if it were reachable, and the empty miss log proves the walker
        // did not merely fail to find a translation — it never looked.
        var snapshot = Snapshot(wholeValues: Vocabulary);
        var log = new RecordingMissLog();

        var row = new InertRow
        {
            Label = "Navami",
            Count = 3,
            Codes = ["Dasami"],
            Child = new InertChild { Caption = "Krishna" }
        };

        new ObjectGraphTranslator(snapshot, log).Walk(row);

        Assert.Equal("Navami", row.Label);
        Assert.Equal(new[] { "Dasami" }, row.Codes);
        Assert.Equal("Krishna", row.Child!.Caption);
        Assert.Equal(0, log.Calls);
    }

    // ------------------------------------------------------------------ memo, blanks, miss log

    private sealed class SimpleRow
    {
        [Translatable(Category = "panchangam")]
        public string? Text { get; set; }
    }

    [Fact]
    public void A_repeated_value_is_resolved_once_and_reused_everywhere()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary);
        var log = new RecordingMissLog();

        var rows = new List<SimpleRow>();
        for (var i = 0; i < 5; i++) rows.Add(new SimpleRow { Text = "Navami" });
        for (var i = 0; i < 5; i++) rows.Add(new SimpleRow { Text = "Sthira Yoga" });

        new ObjectGraphTranslator(snapshot, log).Walk(rows);

        Assert.All(rows.Take(5), r => Assert.Equal("[navami]", r.Text));
        Assert.All(rows.Skip(5), r => Assert.Equal("Sthira Yoga", r.Text));

        // Five identical untranslatable values, one resolution: the memo short-circuits the other
        // four before they can reach the miss log.
        Assert.Equal(1, log.Calls);
    }

    [Fact]
    public void Misses_are_recorded_with_the_category_of_the_property_that_produced_them()
    {
        var snapshot = Snapshot(wholeValues: Vocabulary);
        var log = new RecordingMissLog();

        new ObjectGraphTranslator(snapshot, log).Walk(new SimpleRow { Text = "Sthira Yoga" });

        var miss = Assert.Single(log.Recorded);
        Assert.Equal("Sthira Yoga", miss.Value);
        Assert.Equal("panchangam", miss.Category);
    }

    private sealed class BlankRow
    {
        [Translatable]
        public string? Missing { get; set; }

        [Translatable]
        public string? Blank { get; set; }

        [Translatable]
        public string? Spaces { get; set; }

        [Translatable]
        public List<string> Tags { get; set; } = [];
    }

    [Fact]
    public void Null_empty_and_whitespace_values_are_left_exactly_as_they_were()
    {
        // Whitespace is preserved byte for byte rather than trimmed or emptied — the response is a
        // faithful echo of the database for anything that cannot be translated. And a blank is not a
        // miss: recording it would flood the admin's worklist with nothing.
        var snapshot = Snapshot(wholeValues: Vocabulary);
        var log = new RecordingMissLog();

        var row = new BlankRow
        {
            Missing = null,
            Blank = string.Empty,
            Spaces = "  \t ",
            Tags = ["", "   "]
        };

        new ObjectGraphTranslator(snapshot, log).Walk(row);

        Assert.Null(row.Missing);
        Assert.Equal(string.Empty, row.Blank);
        Assert.Equal("  \t ", row.Spaces);
        Assert.Equal(new[] { "", "   " }, row.Tags);
        Assert.Equal(0, log.Calls);
    }

    private sealed class MemoScopeRow
    {
        /// <summary>Plain: no phrase substitution, so this one stays English.</summary>
        [Translatable]
        public string? Plain { get; set; }

        /// <summary>Composite: the same text here must get phrase substitution.</summary>
        [Translatable(Composite = true, Category = "panchangam")]
        public string? Detail { get; set; }
    }

    /// <summary>
    /// The memo must not let one property's answer leak onto a property that is configured
    /// differently. A plain [Translatable] resolves "Navami upto 14:00" to itself; a Composite
    /// property with the same text must still substitute. Keying the memo on the raw string alone
    /// makes the result depend on reflection property order.
    /// </summary>
    [Fact]
    public void Memo_does_not_leak_across_properties_with_different_composite_settings()
    {
        var snapshot = Snapshot(termsByCategory: new()
        {
            ["panchangam"] = new() { ["Navami"] = "నవమి" }
        });

        var row = new MemoScopeRow { Plain = "Navami upto 14:00", Detail = "Navami upto 14:00" };

        new ObjectGraphTranslator(snapshot, new RecordingMissLog()).Walk(row);

        Assert.Equal("Navami upto 14:00", row.Plain);
        Assert.Equal("నవమి upto 14:00", row.Detail);
    }

    private sealed class MemoCategoryRow
    {
        [Translatable(Composite = true, Category = "panchangam")]
        public string? Tithi { get; set; }

        [Translatable(Composite = true, Category = "masters")]
        public string? Deity { get; set; }
    }

    /// <summary>Two categories that translate the same English word differently must not share a memo entry.</summary>
    [Fact]
    public void Memo_does_not_leak_across_categories()
    {
        var snapshot = Snapshot(termsByCategory: new()
        {
            ["panchangam"] = new() { ["Chitra"] = "చిత్ర-nakshatra" },
            ["masters"] = new() { ["Chitra"] = "చిత్ర-deity" }
        });

        var row = new MemoCategoryRow { Tithi = "Chitra", Deity = "Chitra" };

        new ObjectGraphTranslator(snapshot, new RecordingMissLog()).Walk(row);

        Assert.Equal("చిత్ర-nakshatra", row.Tithi);
        Assert.Equal("చిత్ర-deity", row.Deity);
    }
}

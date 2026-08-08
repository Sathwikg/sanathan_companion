using System.Collections;

namespace Sanathana.Companion.Application.Common.Translation;

/// <summary>
/// Rewrites every <see cref="TranslatableAttribute"/> string in a response object graph, in place.
/// </summary>
/// <remarks>
/// <para>
/// Mutating in place is safe only because every service in this codebase allocates fresh DTOs per
/// request. If a service ever returns a cached or memoised DTO, this walker would write translations
/// into that cache and poison it for other languages — keep DTO construction per-request.
/// </para>
/// <para>Not thread-safe; construct one per response.</para>
/// </remarks>
public sealed class ObjectGraphTranslator
{
    private const int MaxDepth = 8;

    private readonly TranslationSnapshot _snapshot;
    private readonly ITranslationMissLog? _misses;
    private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Raw value -&gt; translated value, for this response only. "Krishna (Bahula)" appears on roughly
    /// half of a year's Panchangam rows; this collapses those into a single match.
    /// </summary>
    /// <remarks>
    /// Keyed on the property's category and composite flag as well as the text, because those change
    /// the answer: a plain [Translatable] leaves "Navami upto 14:00" in English while a Composite one
    /// substitutes inside it, and two categories can translate the same word differently. Keying on
    /// the text alone made the result depend on reflection property order. The hot path is unaffected —
    /// the repeated values that make the memo worth having all sit on the same property.
    /// </remarks>
    private readonly Dictionary<(string Value, string? Category, bool Composite), string> _memo = new();

    public ObjectGraphTranslator(TranslationSnapshot snapshot, ITranslationMissLog? misses = null)
    {
        _snapshot = snapshot;
        _misses = misses;
    }

    public void Walk(object? node) => WalkCore(node, 0);

    private void WalkCore(object? node, int depth)
    {
        if (node is null || depth > MaxDepth) return;
        if (node is string || node is ValueType) return;
        if (!_seen.Add(node)) return; // cycle guard

        switch (node)
        {
            case IDictionary dict:
                foreach (var v in dict.Values) WalkCore(v, depth + 1);
                return;
            case IEnumerable seq:
                foreach (var item in seq) WalkCore(item, depth + 1);
                return;
        }

        var map = TypeMapCache.For(node.GetType());
        if (map.IsInert) return;

        foreach (var p in map.Translatable)
        {
            switch (p.Kind)
            {
                case PropKind.String:
                {
                    var current = (string?)p.Get(node);
                    var translated = Resolve(current, p, node);
                    if (!ReferenceEquals(current, translated)) p.Set(node, translated);
                    break;
                }
                case PropKind.StringList:
                {
                    if (p.Get(node) is IList<string> list && list.Count > 0)
                        for (var i = 0; i < list.Count; i++)
                            list[i] = Resolve(list[i], p, node) ?? list[i];
                    break;
                }
            }
        }

        foreach (var child in map.Children) WalkCore(child.Get(node), depth + 1);
    }

    /// <summary>The resolution chain: row override, whole-value, phrase substitution, original.</summary>
    private string? Resolve(string? value, TranslatableProperty p, object owner)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        // 1. Per-row override wins outright — it is the most specific answer available.
        //    Not memoised: two rows can share English text but need different translations.
        if (p.Attr.EntityType is { } entityType && p.GetKey is not null)
        {
            var key = p.GetKey(owner)?.ToString();
            if (!string.IsNullOrWhiteSpace(key) &&
                _snapshot.TryGetEntity(TranslationSnapshot.EntityKey(entityType, key!, p.Field), out var rowText))
                return rowText;
        }

        var memoKey = (value, p.Attr.Category, p.Attr.Composite);
        if (_memo.TryGetValue(memoKey, out var cached)) return cached;

        string result;

        // 2. Whole-value match — the fast path for controlled vocabulary.
        if (_snapshot.TryGetWholeValue(TermMatcher.NormaliseKey(value), out var whole))
        {
            result = whole;
        }
        else if (p.Attr.Composite)
        {
            // 3. Phrase substitution, for values that embed terms among times and numbers.
            result = _snapshot.MatcherFor(p.Attr.Category).Translate(value);
        }
        else
        {
            // 4. No translation — leave the English exactly as it was.
            result = value;
        }

        // Nothing changed: the app wanted to show this and could not translate it. Log it so it
        // surfaces in the admin's worklist, however obscure the source.
        if (ReferenceEquals(result, value) || string.Equals(result, value, StringComparison.Ordinal))
            _misses?.Record(value, p.Attr.Category);

        _memo[memoKey] = result;
        return result;
    }
}

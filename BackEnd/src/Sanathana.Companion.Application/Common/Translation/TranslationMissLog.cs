using System.Collections.Concurrent;

namespace Sanathana.Companion.Application.Common.Translation;

public sealed record TranslationMiss(string Value, string? Category, int Count);

/// <summary>
/// Records values that reached a translatable property but could not be translated.
/// </summary>
/// <remarks>
/// This is what makes the dictionary genuinely self-maintaining. Column scanning can only find
/// text that is stored somewhere; the Panchangam "compute for my location" endpoint returns strings
/// that exist in no table at all, and a future form might do the same. Anything the app actually
/// tried to show and could not translate ends up here, ranked by how often users hit it.
/// </remarks>
public interface ITranslationMissLog
{
    void Record(string value, string? category);

    /// <summary>Takes and clears the current misses.</summary>
    IReadOnlyList<TranslationMiss> Drain();

    int Count { get; }
}

/// <inheritdoc />
public sealed class TranslationMissLog : ITranslationMissLog
{
    /// <summary>Bounded so a pathological response can never grow this without limit.</summary>
    private const int MaxEntries = 5_000;

    private readonly ConcurrentDictionary<string, Entry> _misses = new(StringComparer.Ordinal);

    private sealed class Entry
    {
        public required string Value { get; init; }
        public string? Category { get; init; }
        public int Count;
    }

    public int Count => _misses.Count;

    public void Record(string value, string? category)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var key = TermMatcher.NormaliseKey(value);
        if (key.Length == 0) return;

        if (_misses.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref existing.Count);
            return;
        }

        // Stop accepting NEW keys past the cap, but keep counting the ones already tracked.
        if (_misses.Count >= MaxEntries) return;

        _misses.TryAdd(key, new Entry { Value = value.Trim(), Category = category, Count = 1 });
    }

    public IReadOnlyList<TranslationMiss> Drain()
    {
        var snapshot = _misses.ToArray();
        foreach (var kv in snapshot) _misses.TryRemove(kv.Key, out _);

        return snapshot
            .Select(kv => new TranslationMiss(kv.Value.Value, kv.Value.Category, kv.Value.Count))
            .OrderByDescending(m => m.Count)
            .ToList();
    }
}

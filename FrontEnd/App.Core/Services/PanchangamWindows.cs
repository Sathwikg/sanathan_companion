using System.Globalization;
using System.Text.RegularExpressions;
using App.Core.Models;

namespace App.Core.Services;

/// <summary>The seven Panchangam windows the phone home watches.</summary>
public enum PanchangamWindowKind { RahuKalam, Yamagandam, Gulika, Durmuhurtham, Varjyam, AmruthaKalam, AbhijitMuhurtham }

/// <summary>Declaration order is the precedence when windows overlap: the warning a seeker must not miss wins.</summary>
public enum PanchangamTone { Red = 0, Amber = 1, Green = 2 }

/// <summary>One window as absolute IST instants, start inclusive, end exclusive.</summary>
public sealed record PanchangamWindow(PanchangamWindowKind Kind, DateTime Start, DateTime End)
{
    public PanchangamTone Tone => PanchangamWindows.ToneOf(Kind);
    public bool Contains(DateTime now) => Start <= now && now < End;
}

/// <summary>
/// One tithi or nakshatram span: the server's own (localized) text for it, and when it runs. A null
/// Start means "since before the rows we have"; a null End means "until after them".
/// </summary>
public sealed record PanchangamSpan(string Text, DateTime? Start, DateTime? End)
{
    public bool Contains(DateTime now) => (Start is null || Start <= now) && (End is null || now < End);
}

/// <summary>What the card says right now.</summary>
public sealed record PanchangamSnapshot(
    PanchangamWindow? Active,
    PanchangamWindow? Secondary,
    PanchangamWindow? Next,
    DateTime? NextChange,
    bool MissingYesterday,
    bool MissingTomorrow,
    bool Unknown)
{
    public PanchangamTone? Tone => Active?.Tone;
    public static readonly PanchangamSnapshot Empty = new(null, null, null, null, true, true, true);
}

/// <summary>
/// Reads the server's Panchangam window text and decides what is happening at a given IST instant.
///
/// The text comes from ONE formatter (PanchangamCalculator.Range), for stored and computed rows
/// alike: "HH:mm – HH:mm" (spaces around an en dash), several windows joined by ", ", and
/// "HH:mm, dd MMM" for an instant on a later civil day than the row's. Translation swaps only
/// words — "upto", "from", "full day" and the whole-value "None" (te "లేదు", hi "कोई नहीं", ta
/// "இல்லை", kn "ಇಲ್ಲ") — and leaves digits, the colon, the dash, the comma and the day suffix
/// byte-identical, so only clock times are read here and no word is ever matched. The month token
/// is the SERVER's culture ("Sep" or "Sept") and is deliberately ignored; the day number is enough.
///
/// A window that spills past midnight lives in the PREVIOUS day's row, and the first window after
/// the day's last one lives in TOMORROW's row, so callers hand in yesterday's, today's and
/// tomorrow's rows together. Pure and clock-free, so the rules can be pinned in App.Tests.
/// </summary>
public static class PanchangamWindows
{
    public static PanchangamTone ToneOf(PanchangamWindowKind kind) => kind switch
    {
        PanchangamWindowKind.RahuKalam => PanchangamTone.Red,
        PanchangamWindowKind.Yamagandam or PanchangamWindowKind.Gulika
            or PanchangamWindowKind.Durmuhurtham or PanchangamWindowKind.Varjyam => PanchangamTone.Amber,
        _ => PanchangamTone.Green
    };

    /// <summary>A CSS token for the kind; never UI text (the names are localized in Razor).</summary>
    public static string Slug(PanchangamWindowKind kind) => kind switch
    {
        PanchangamWindowKind.RahuKalam => "rahu",
        PanchangamWindowKind.Yamagandam => "yama",
        PanchangamWindowKind.Gulika => "gulika",
        PanchangamWindowKind.Durmuhurtham => "durmuhurtham",
        PanchangamWindowKind.Varjyam => "varjyam",
        PanchangamWindowKind.AmruthaKalam => "amrutha",
        _ => "abhijit"
    };

    /// <summary>
    /// INSTANT = HH:mm[, dd MMM]. The suffix needs whitespace after the day number, so the ", 14:55"
    /// that joins two windows is never mistaken for one.
    /// </summary>
    private static readonly Regex Instant = new(
        @"(?<![0-9:])(?<h>[01]?[0-9]|2[0-3]):(?<m>[0-5][0-9])(?![0-9:])(?:,\s*(?<dd>[0-9]{1,2})\s+(?<mon>[^\s,]+))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>", " that starts a new tithi/nakshatram segment — not the ", dd MMM" day suffix.</summary>
    private static readonly Regex SegmentBreak = new(@",\s(?![0-9]{1,2}\s)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The ", dd MMM" after a clock time, for the compact card where the day is obvious.</summary>
    private static readonly Regex DaySuffix = new(@"(?<=[0-9]:[0-9]{2}(?:[\s\u00A0]?[AP]M)?),\s*[0-9]{1,2}\s+[^\s,]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Every window in one field, anchored to the row's date. Empty for null, blank or "None" in any language.</summary>
    public static IReadOnlyList<PanchangamWindow> Parse(PanchangamWindowKind kind, string? text, DateOnly rowDate)
    {
        var result = new List<PanchangamWindow>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var instants = Instants(text, rowDate);
        for (var i = 0; i + 1 < instants.Count; i += 2)
        {
            var start = instants[i];
            var end = instants[i + 1];
            // The formatter dates the Tuesday-night Durmuhurtham from SUNSET, so for coordinates
            // whose sunset is after IST midnight the end can carry no suffix and read before the
            // start. Never for Indian rows; harmless to guard.
            if (end <= start) end = end.AddDays(1);
            result.Add(new PanchangamWindow(kind, start, end));
        }
        return result;
    }

    /// <summary>The seven fields of one row.</summary>
    public static IReadOnlyList<PanchangamWindow> ParseRow(PanchangamModel row)
    {
        var all = new List<PanchangamWindow>();
        all.AddRange(Parse(PanchangamWindowKind.RahuKalam, row.RahuKalam, row.Date));
        all.AddRange(Parse(PanchangamWindowKind.Yamagandam, row.Yamagandam, row.Date));
        all.AddRange(Parse(PanchangamWindowKind.Gulika, row.Gulika, row.Date));
        all.AddRange(Parse(PanchangamWindowKind.Durmuhurtham, row.Durmuhurtham, row.Date));
        all.AddRange(Parse(PanchangamWindowKind.Varjyam, row.Varjyam, row.Date));
        all.AddRange(Parse(PanchangamWindowKind.AmruthaKalam, row.AmruthaKalam, row.Date));
        all.AddRange(Parse(PanchangamWindowKind.AbhijitMuhurtham, row.AbhijitMuhurtham, row.Date));
        return all;
    }

    /// <summary>
    /// Every window of every row, de-duplicated: a window that straddles a sunrise is printed by
    /// both that day's row and the previous one, each rounded on its own, so a minute of slack.
    /// </summary>
    public static IReadOnlyList<PanchangamWindow> AllWindows(IEnumerable<PanchangamModel> rows)
    {
        var kept = new List<PanchangamWindow>();
        foreach (var w in rows.SelectMany(ParseRow).OrderBy(w => w.Start).ThenBy(w => w.Kind))
        {
            var dup = kept.Any(k => k.Kind == w.Kind
                && Math.Abs((k.Start - w.Start).TotalMinutes) <= 1
                && Math.Abs((k.End - w.End).TotalMinutes) <= 1);
            if (!dup) kept.Add(w);
        }
        return kept;
    }

    /// <summary>
    /// What is active at <paramref name="now"/> across the rows given (yesterday, today, tomorrow):
    /// the strongest tone first, then the one ending soonest; a second active window of another
    /// kind as Secondary; the earliest window still ahead as Next; and the next instant at which any
    /// of this changes, for a timer.
    /// </summary>
    public static PanchangamSnapshot Evaluate(IEnumerable<PanchangamModel> rows, DateTime now)
    {
        var list = rows.Where(r => r is not null).ToList();
        var today = DateOnly.FromDateTime(now);
        var todayRow = list.FirstOrDefault(r => r.Date == today);
        if (todayRow is null) return PanchangamSnapshot.Empty;

        var hasYesterday = list.Any(r => r.Date == today.AddDays(-1));
        var hasTomorrow = list.Any(r => r.Date == today.AddDays(1));
        var windows = AllWindows(list);

        var active = windows.Where(w => w.Contains(now)).OrderBy(w => w.Tone).ThenBy(w => w.End).ToList();
        var primary = active.FirstOrDefault();
        var secondary = primary is null ? null : active.FirstOrDefault(w => w.Kind != primary.Kind);
        var next = windows.Where(w => w.Start > now).OrderBy(w => w.Start).ThenBy(w => w.Tone).FirstOrDefault();
        DateTime? nextChange = windows
            .SelectMany(w => new[] { w.Start, w.End })
            .Where(t => t > now)
            .OrderBy(t => t)
            .Cast<DateTime?>()
            .FirstOrDefault();

        // Between midnight and sunrise everything that matters is in yesterday's row; without it
        // nothing can honestly be claimed, so the card shows only the facts.
        var beforeSunrise = todayRow.Sunrise is { } sunrise && TimeOnly.FromDateTime(now) < sunrise;
        var unknown = beforeSunrise && !hasYesterday && primary is null;

        return new PanchangamSnapshot(primary, secondary, next, nextChange, !hasYesterday, !hasTomorrow, unknown);
    }

    /// <summary>
    /// The tithi (or nakshatram) segments of one row: "X upto T" ends at T, "X from T" starts at T,
    /// "X full day" is the whole row. Texts stay exactly as the server sent them.
    /// </summary>
    public static IReadOnlyList<PanchangamSpan> Segments(string? details, DateOnly rowDate)
    {
        var result = new List<PanchangamSpan>();
        if (string.IsNullOrWhiteSpace(details)) return result;

        var parts = SegmentBreak.Split(details.Trim());
        DateTime? previousEnd = null;
        for (var i = 0; i < parts.Length; i++)
        {
            var text = parts[i].Trim();
            if (text.Length == 0) continue;
            var at = Instants(text, rowDate).Cast<DateTime?>().FirstOrDefault();
            if (i < parts.Length - 1)
            {
                result.Add(new PanchangamSpan(text, previousEnd, at));
                previousEnd = at;
            }
            else
            {
                // The last segment, "X from T" or "X full day". It starts where the previous one ended:
                // the server's "from T" is just the minute after the preceding "upto", and anchoring on
                // it would leave that minute with no span at all.
                result.Add(new PanchangamSpan(text, previousEnd ?? at, null));
            }
        }
        return result;
    }

    /// <summary>
    /// One timeline from consecutive rows. A row's last segment ("X from T") and the next row's first
    /// ("X upto T2") are the same span cut at the row boundary, so they are joined, keeping the
    /// text that carries the end time.
    /// </summary>
    public static IReadOnlyList<PanchangamSpan> Timeline(IEnumerable<PanchangamModel> rows, Func<PanchangamModel, string?> field)
    {
        var merged = new List<PanchangamSpan>();
        foreach (var row in rows.Where(r => r is not null).OrderBy(r => r.Date))
        {
            var first = true;
            foreach (var span in Segments(field(row), row.Date))
            {
                if (first && span.Start is null && merged.Count > 0 && merged[^1].End is null)
                    merged[^1] = new PanchangamSpan(span.Text, merged[^1].Start, span.End);
                else
                    merged.Add(span);
                first = false;
            }
        }
        return merged;
    }

    /// <summary>The span running at <paramref name="now"/>, or null when the rows do not cover it.</summary>
    public static PanchangamSpan? Current(IEnumerable<PanchangamModel> rows, Func<PanchangamModel, string?> field, DateTime now)
        => Timeline(rows, field).FirstOrDefault(s => s.Contains(now));

    /// <summary>
    /// A span's text for the compact card: clock times in 12-hour and the ", dd MMM" day marker
    /// dropped — the current span's end is obviously the next time that clock shows.
    /// </summary>
    public static string Compact(string text) => DaySuffix.Replace(ClockText.To12Hour(text), string.Empty);

    private static List<DateTime> Instants(string text, DateOnly rowDate)
    {
        var list = new List<DateTime>();
        foreach (Match m in Instant.Matches(text))
        {
            var time = new TimeOnly(
                int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture));
            var date = m.Groups["dd"].Success
                ? NextDateWithDay(rowDate, int.Parse(m.Groups["dd"].Value, CultureInfo.InvariantCulture))
                : rowDate;
            list.Add(date.ToDateTime(time));
        }
        return list;
    }

    /// <summary>The suffix is only ever a LATER day than the row's (a 31 Dec row says ", 01 Jan").</summary>
    private static DateOnly NextDateWithDay(DateOnly rowDate, int day)
    {
        var d = rowDate.AddDays(1);
        for (var i = 0; i < 62; i++)
        {
            if (d.Day == day) return d;
            d = d.AddDays(1);
        }
        return rowDate.AddDays(1);
    }
}
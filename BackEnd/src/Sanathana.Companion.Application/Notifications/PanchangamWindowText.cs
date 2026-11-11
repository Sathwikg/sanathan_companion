using System.Text.RegularExpressions;

namespace Sanathana.Companion.Application.Notifications;

/// <summary>One Panchangam window as IST wall-clock instants, start inclusive, end exclusive.</summary>
public readonly record struct PanchangamWindowSpan(DateTime Start, DateTime End);

/// <summary>
/// Reads the window text PanchangamCalculator.Range writes ("10:30 – 12:00", several joined by
/// ", ", and "01:10, 14 Nov" for an instant on a later civil day than the row's) back into instants.
/// </summary>
/// <remarks>
/// The phone has the same reader (App.Core's PanchangamWindows) for its home card; this is the
/// server's, for the warnings. Only clock times and the day number are read — never a word — so a
/// row stored as "None" or left empty yields nothing, and the month token (whose spelling depends on
/// the culture the row was generated under) is ignored: the day number says which day.
/// </remarks>
public static partial class PanchangamWindowText
{
    [GeneratedRegex(@"(?<![0-9:])(?<h>[01]?[0-9]|2[0-3]):(?<m>[0-5][0-9])(?![0-9:])(?:,\s*(?<dd>[0-9]{1,2})\s+[^\s,]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex Instant();

    public static IReadOnlyList<PanchangamWindowSpan> Parse(string? text, DateOnly rowDate)
    {
        var result = new List<PanchangamWindowSpan>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var instants = new List<DateTime>();
        foreach (Match m in Instant().Matches(text))
        {
            var time = new TimeOnly(int.Parse(m.Groups["h"].Value), int.Parse(m.Groups["m"].Value));
            var date = rowDate;

            if (m.Groups["dd"].Success && int.TryParse(m.Groups["dd"].Value, out var day))
            {
                // The suffix names a later civil day. A window never runs more than a day or two past
                // its row, so the first date on or after the row's with that day number is the one.
                for (var i = 0; i <= 3; i++)
                {
                    var candidate = rowDate.AddDays(i);
                    if (candidate.Day == day) { date = candidate; break; }
                }
            }

            instants.Add(date.ToDateTime(time));
        }

        for (var i = 0; i + 1 < instants.Count; i += 2)
        {
            var start = instants[i];
            var end = instants[i + 1];

            // An end that reads earlier than its start crossed midnight without saying so.
            if (end <= start) end = end.AddDays(1);
            result.Add(new PanchangamWindowSpan(start, end));
        }

        return result;
    }

    /// <summary>
    /// The first segment of a tithi or nakshatram field ("Dashami upto 14:05, Ekadashi" gives
    /// "Dashami upto 14:05"): the one in force at sunrise, which is what a morning summary names.
    /// </summary>
    public static string FirstSegment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // ", " starts a new segment unless it is the ", dd MMM" day suffix after a clock time.
        var parts = SegmentBreak().Split(text.Trim(), 2);
        return parts[0].Trim();
    }

    [GeneratedRegex(@",\s(?![0-9]{1,2}\s)", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentBreak();
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace App.Core.Services;

/// <summary>
/// Clock times and dates the way the app prints them: 12-hour with English AM/PM and English day
/// and month names, in every language and on every device. Nothing pins a culture in either host
/// and MAUI follows the OS locale, so a plain ToString("h:mm tt") could print Tamil or Kannada
/// AM/PM words; everything here formats with the invariant culture instead.
/// </summary>
public static class ClockText
{
    public const string Nbsp = "\u00A0";

    /// <summary>
    /// "1:36 PM – 3:05 PM": no-break spaces inside each time and around the dash, plus a word joiner
    /// after the dash — Unicode allows a break after an en dash even when a no-break space follows.
    /// </summary>
    public const string RangeDash = "\u00A0–\u2060\u00A0";

    /// <summary>
    /// A 24-hour clock time such as 19:14 or 07:27, and nothing that merely contains a colon. ASCII
    /// digits only: \d would also match Telugu or Devanagari digits that int.Parse then rejects. A
    /// time already followed by AM/PM is left alone, so a value can be rewritten twice without harm.
    /// </summary>
    public static readonly Regex ClockTime = new(
        @"(?<![0-9:])(?<h>[01]?[0-9]|2[0-3]):(?<m>[0-5][0-9])(?![0-9:])(?![\s\u00A0]?[AP]M)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Every clock time in a value, 24-hour to 12-hour: "Vidiya upto 19:14" becomes "Vidiya upto
    /// 7:14 PM", "07:27 – 08:58" becomes "7:27 AM – 8:58 AM". The API sends Tithi, Rahu Kalam and
    /// the rest as ready-made text, so the times are rewritten inside it. AM/PM stay in English in
    /// every language, like the rest of the app's dates and times.
    /// </summary>
    /// <param name="separator">
    /// Between the time and AM/PM. On screen a no-break space (the default), because a plain space
    /// let "7:00 / AM" break across lines; the share picture wraps on its own and passes " ".
    /// </param>
    public static string To12Hour(string value, string? separator = null) => ClockTime.Replace(value, m =>
        To12Hour(int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture),
                 int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture), separator));

    public static string To12Hour(int hour, int minute, string? separator = null)
    {
        var h12 = hour % 12 == 0 ? 12 : hour % 12;
        return $"{h12}:{minute.ToString("00", CultureInfo.InvariantCulture)}{separator ?? Nbsp}{(hour < 12 ? "AM" : "PM")}";
    }

    public static string Time(TimeOnly t, string? separator = null) => To12Hour(t.Hour, t.Minute, separator);

    public static string Time(DateTime t, string? separator = null) => To12Hour(t.Hour, t.Minute, separator);

    public static string Range(DateTime start, DateTime end) => Time(start) + RangeDash + Time(end);

    /// <summary>"Thu, 15 Oct 2026" — English whatever the device locale.</summary>
    public static string ShortDate(DateOnly d) => d.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Thu, 15 Oct" — the greeting line, where the year is noise.</summary>
    public static string DayMonth(DateOnly d) => d.ToString("ddd, dd MMM", CultureInfo.InvariantCulture);

    /// <summary>"Thursday, 15 October 2026".</summary>
    public static string LongDate(DateOnly d) => d.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture);
}
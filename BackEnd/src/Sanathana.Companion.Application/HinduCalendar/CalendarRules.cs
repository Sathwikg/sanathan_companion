using Sanathana.Companion.Application.Panchangam;

namespace Sanathana.Companion.Application.HinduCalendar;

/// <summary>How a day reads for ordinary undertakings. The grid tints by it.</summary>
public enum DayRating { Caution = -1, Neutral = 0, Good = 1 }

/// <summary>The rating, the score behind it, and the reason codes the client puts into words.</summary>
public sealed record DayAssessment(DayRating Rating, int Score, IReadOnlyList<string> Reasons);

/// <summary>
/// Reads the Panchangam's own text back into tithi and nakshatra indices and derives from them what
/// a month grid shows: the moon's phase, the observances a tithi carries (Purnima, Ekadashi,
/// Pradosham …) and a plain good / neutral / caution reading of the day.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is a pure function of the calculator's output, so stored rows and on-the-fly
/// results read identically and the rules can be pinned in tests. The names are matched against
/// <see cref="PanchangamTables"/>, the one place the calculator takes them from, so a renamed
/// tithi cannot silently stop matching.
/// </para>
/// <para>
/// The rating is a general reading, in the manner of a printed calendar's shubha / ashubha tick:
/// the tithi class (rikta tithis are avoided for beginnings), the nakshatra's traditional
/// gentleness or harshness, the weekday, and whether a festival falls that day. It is deliberately
/// NOT a muhurtha (no lagna, no yoga, no karana) and the calendar says so in its legend.
/// </para>
/// </remarks>
public static class CalendarRules
{
    /// <summary>Tithi index 0..29: 0 is Shukla Padyami, 14 Pournami, 15 Krishna Padyami, 29 Amavasya.</summary>
    public const int Pournami = 14;
    public const int Amavasya = 29;

    public const string RatingGood = "good";
    public const string RatingNeutral = "neutral";
    public const string RatingCaution = "caution";

    // ------------------------------------------------------------------ reading the text back

    /// <summary>
    /// The tithi running at sunrise, from the stored "Dasami upto 13:58, Ekadasi from 13:58" text
    /// and the row's paksham. False when the text is not the calculator's.
    /// </summary>
    public static bool TryTithiIndex(string? tithiDetails, string? paksham, out int index)
    {
        index = -1;
        var name = FirstName(tithiDetails);
        if (name is null) return false;

        if (name.Equals("Amavasya", StringComparison.OrdinalIgnoreCase))
        {
            index = Amavasya;
            return true;
        }

        var i = Array.FindIndex(PanchangamTables.Tithis, t => t.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (i < 0) return false;

        var krishna = paksham?.StartsWith("Krishna", StringComparison.OrdinalIgnoreCase) == true;
        // "Pournami" is only ever written in Shukla paksha; the calculator writes Amavasya for 29.
        if (krishna && i == Pournami) return false;

        index = krishna ? i + 15 : i;
        return true;
    }

    /// <summary>The nakshatra running at sunrise, 0..26, from "Rohini upto 09:12, Mrigasira from 09:12".</summary>
    public static bool TryNakshatraIndex(string? nakshatramDetails, out int index)
    {
        index = -1;
        var name = FirstName(nakshatramDetails);
        if (name is null) return false;
        index = Array.FindIndex(PanchangamTables.Nakshatras, n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0;
    }

    /// <summary>
    /// The element named first in a details string: the text before the first comma, with the
    /// "upto …", "from …" and "full day" tail removed. Words only; the clock times are never read.
    /// </summary>
    public static string? FirstName(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return null;

        var first = details.Split(',', 2)[0].Trim();
        foreach (var tail in new[] { " upto ", " from ", " full day" })
        {
            var at = first.IndexOf(tail, StringComparison.OrdinalIgnoreCase);
            if (at > 0) first = first[..at];
        }
        first = first.Trim();
        return first.Length == 0 ? null : first;
    }

    /// <summary>The tithi name as the calculator writes it, by 0..29 index.</summary>
    public static string TithiName(int index)
        => index == Amavasya ? "Amavasya" : PanchangamTables.Tithis[index % 15];

    // ------------------------------------------------------------------ the moon

    /// <summary>
    /// A phase code for the glyph: new, waxingCrescent, firstQuarter, waxingGibbous, full,
    /// waningGibbous, lastQuarter, waningCrescent. Codes, not glyphs, so the client chooses the art.
    /// </summary>
    public static string MoonPhase(int tithiIndex) => tithiIndex switch
    {
        Amavasya or 0 => "new",
        <= 5 => "waxingCrescent",
        <= 8 => "firstQuarter",
        <= 13 => "waxingGibbous",
        Pournami => "full",
        <= 20 => "waningGibbous",
        <= 23 => "lastQuarter",
        _ => "waningCrescent"
    };

    /// <summary>Lit fraction of the disc, 0..100, from the tithi's mid-point elongation.</summary>
    public static int MoonIllumination(int tithiIndex)
    {
        var elongation = (tithiIndex + 0.5) * 12.0 * Math.PI / 180.0;
        return (int)Math.Round(50.0 * (1.0 - Math.Cos(elongation)));
    }

    // ------------------------------------------------------------------ observances

    /// <summary>
    /// The observances a tithi carries every month, as codes the client localises: purnima,
    /// amavasya, ekadashi, pradosham, vinayakaChaturthi, sankashtiChaturthi, masaShivaratri,
    /// skandaShashti, durgaAshtami. Named festivals come from the Festivals master, not from here.
    /// </summary>
    public static IReadOnlyList<string> Markers(int tithiIndex)
    {
        var list = new List<string>(2);
        switch (tithiIndex)
        {
            case Pournami: list.Add("purnima"); break;
            case Amavasya: list.Add("amavasya"); break;
            case 10 or 25: list.Add("ekadashi"); break;
            case 12 or 27: list.Add("pradosham"); break;
            case 3: list.Add("vinayakaChaturthi"); break;
            case 18: list.Add("sankashtiChaturthi"); break;
            case 28: list.Add("masaShivaratri"); break;
            case 5: list.Add("skandaShashti"); break;
            case 7: list.Add("durgaAshtami"); break;
        }
        return list;
    }

    // ------------------------------------------------------------------ the day's reading

    /// <summary>
    /// Per-tithi score, 0..29. Rikta tithis (Chaviti, Navami, Chaturdasi) are avoided for
    /// beginnings; Pournami is the most favoured; Amavasya is kept for ancestors rather than
    /// ventures; Ashtami and Padyami are middling.
    /// </summary>
    private static readonly int[] TithiScore =
    {
        //  Padyami Vidiya Tadiya Chaviti Panchami Shashti Saptami Ashtami Navami Dasami Ekadasi Dvadasi Trayodasi Chaturdasi Pournami
            0,      1,     1,     -2,     1,       0,      1,      -1,     -2,    1,     1,      1,      1,        -2,        2,
        //  Padyami Vidiya Tadiya Chaviti Panchami Shashti Saptami Ashtami Navami Dasami Ekadasi Dvadasi Trayodasi Chaturdasi Amavasya
            0,      1,     1,     -2,     1,       0,      1,      -1,     -2,    1,     1,      1,      1,        -2,        -1
    };

    /// <summary>
    /// Per-nakshatra score, 0..26, by the traditional grouping: the gentle and fixed stars favour
    /// undertakings; the fierce and sharp ones do not. Satabhisham is read as neutral.
    /// </summary>
    private static readonly int[] NakshatraScore =
    {
        //  Aswini Bharani Krittika Rohini Mrigasira Arudra Punarvasu Pushyami Aslesha
            1,     -1,     -1,      1,     1,        -1,    1,        1,       -1,
        //  Makha Pubba Uttara Hasta Chitta Swati Visakha Anuradha Jyeshta
            -1,   -1,   1,     1,    1,     1,    -1,     1,       -1,
        //  Moola Purvashada Uttarashada Sravanam Dhanishta Satabhisham Purvabhadra Uttarabhadra Revati
            -1,   -1,        1,          1,       1,        0,          -1,         1,           1
    };

    /// <summary>
    /// Scores the day. Null indices (text that could not be read) simply contribute nothing, so a
    /// day with no Panchangam still gets its weekday and festival reading.
    /// </summary>
    public static DayAssessment Assess(int? tithiIndex, int? nakshatraIndex, DayOfWeek dayOfWeek, bool hasFestival)
    {
        var score = 0;
        var reasons = new List<string>(4);

        if (tithiIndex is >= 0 and <= 29)
        {
            var t = TithiScore[tithiIndex.Value];
            score += t;
            if (tithiIndex == Pournami) reasons.Add("purnima");
            else if (tithiIndex == Amavasya) reasons.Add("tithiAmavasya");
            else if (t <= -2) reasons.Add("tithiRikta");
            else if (t < 0) reasons.Add("tithiAshtami");
            else if (t > 0) reasons.Add("tithiAuspicious");
        }

        if (nakshatraIndex is >= 0 and <= 26)
        {
            var n = NakshatraScore[nakshatraIndex.Value];
            score += n;
            if (n > 0) reasons.Add("nakshatraAuspicious");
            else if (n < 0) reasons.Add("nakshatraHarsh");
        }

        switch (dayOfWeek)
        {
            case DayOfWeek.Tuesday or DayOfWeek.Saturday:
                score -= 1;
                reasons.Add("weekdayHarsh");
                break;
            case DayOfWeek.Thursday or DayOfWeek.Friday:
                score += 1;
                reasons.Add("weekdayAuspicious");
                break;
        }

        if (hasFestival)
        {
            score += 1;
            reasons.Add("festival");
        }

        var rating = score >= 2 ? DayRating.Good : score <= -2 ? DayRating.Caution : DayRating.Neutral;

        // A festival is kept whatever the tithi and star say; telling a seeker to be wary on Diwali
        // would be absurd. The score and the reasons still say what the day is made of.
        if (hasFestival && rating == DayRating.Caution) rating = DayRating.Neutral;

        return new DayAssessment(rating, score, reasons);
    }

    public static string RatingCode(DayRating rating) => rating switch
    {
        DayRating.Good => RatingGood,
        DayRating.Caution => RatingCaution,
        _ => RatingNeutral
    };
}

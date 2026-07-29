using System.Globalization;
using static Sanathana.Companion.Application.Panchangam.PanchangamTables;

namespace Sanathana.Companion.Application.Panchangam;

/// <summary>Everything computed for one date at one location.</summary>
public sealed class PanchangamDay
{
    public DateOnly Date { get; init; }
    public string DayOfWeek { get; init; } = string.Empty;
    public string Samvatsaram { get; init; } = string.Empty;
    public string Ayanam { get; init; } = string.Empty;
    public int SakaYear { get; init; }
    public int VikramaYear { get; init; }
    public string Masam { get; init; } = string.Empty;
    public string Paksham { get; init; } = string.Empty;
    public string Rutuvu { get; init; } = string.Empty;
    public TimeOnly Sunrise { get; init; }
    public TimeOnly Sunset { get; init; }
    public string TithiFormatted { get; init; } = string.Empty;
    public string NakshatramFormatted { get; init; } = string.Empty;
    public string AmruthaKalam { get; init; } = string.Empty;
    public string AbhijitMuhurtham { get; init; } = string.Empty;
    public string Durmuhurtham { get; init; } = string.Empty;
    public string RahuKalam { get; init; } = string.Empty;
    public string Yamagandam { get; init; } = string.Empty;
    public string Varjyam { get; init; } = string.Empty;
    public string Gulika { get; init; } = string.Empty;
}

/// <summary>
/// Derives a full day's Panchangam from the Meeus astronomy in <see cref="Astro"/>.
/// The civil day runs sunrise → next sunrise, which is why elements are labelled by
/// what is running at sunrise and may legitimately span two or three segments.
/// </summary>
public static class PanchangamCalculator
{
    const double IstOffsetHours = 5.5;
    const double TithiArc = 12.0;
    static readonly double NakshatraArc = 360.0 / 27.0;

    public static PanchangamDay Compute(DateOnly date, double latitude, double longitude)
    {
        double jdMidnightUt = Astro.JulianDay(date.Year, date.Month, date.Day) - IstOffsetHours / 24.0;

        var (riseOpt, setOpt) = Astro.SunRiseSet(jdMidnightUt, latitude, longitude);
        double rise = riseOpt ?? jdMidnightUt + 0.25;
        double set = setOpt ?? jdMidnightUt + 0.75;

        var nextMidnight = jdMidnightUt + 1.0;
        var (nextRiseOpt, _) = Astro.SunRiseSet(nextMidnight, latitude, longitude);
        double nextRise = nextRiseOpt ?? rise + 1.0;

        int dow = (int)date.DayOfWeek;                     // 0 = Sunday
        double dayLen = set - rise;
        double nightLen = nextRise - set;

        // ---- tithi & nakshatra running at sunrise, with their true end instants ----
        var tithiSpans = Spans(rise, nextRise, Elongation, TithiArc, 30);
        var nakSpans = Spans(rise, nextRise, MoonLongitude, NakshatraArc, 27);

        int tithiIdx = tithiSpans[0].Index;                // 0-based 0..29
        int nakIdx = nakSpans[0].Index;                    // 0-based 0..26
        bool shukla = tithiIdx < 15;

        // ---- month / year context ----
        var (masamIdx, isAdhika) = ResolveMasam(rise);
        int saka = ResolveSakaYear(rise);
        string samvatsara = Samvatsaras[((saka + 11) % 60 + 60) % 60];

        return new PanchangamDay
        {
            Date = date,
            DayOfWeek = WeekdaysEnglish[dow],
            Samvatsaram = samvatsara,
            Ayanam = ResolveAyanam(rise),
            SakaYear = saka,
            VikramaYear = saka + 135,
            Masam = (isAdhika ? "Adhika " : string.Empty) + Masams[masamIdx],
            Paksham = shukla ? "Shukla" : "Krishna (Bahula)",
            Rutuvu = RutuvuByMasam[masamIdx],
            Sunrise = ToTime(rise),
            Sunset = ToTime(set),
            TithiFormatted = FormatSpans(tithiSpans, i => TithiName(i), rise),
            NakshatramFormatted = FormatSpans(nakSpans, i => Nakshatras[i], rise),
            RahuKalam = Eighth(rise, dayLen, RahuKalamPart[dow]),
            Yamagandam = Eighth(rise, dayLen, YamagandamPart[dow]),
            Gulika = Eighth(rise, dayLen, GulikaPart[dow]),
            AbhijitMuhurtham = dow == 3 ? "None" : Muhurta(rise, dayLen, AbhijitMuhurtaIndex),
            Durmuhurtham = string.Join(", ", Durmuhurtham[dow]
                .Select(s => s.IsNight ? Muhurta(set, nightLen, s.Index) : Muhurta(rise, dayLen, s.Index))),
            Varjyam = NakshatraWindows(nakSpans, rise, i => VarjyamGhatikas[i]),
            AmruthaKalam = NakshatraWindows(nakSpans, rise, i => new[] { AmruthaGhatikas[i] })
        };
    }

    // ---------- element spans ----------

    readonly record struct Span(int Index, double Start, double End);

    static double Elongation(double jd) => Astro.Norm360(Astro.MoonSidereal(jd) - Astro.SunSidereal(jd));
    static double MoonLongitude(double jd) => Astro.MoonSidereal(jd);

    /// <summary>
    /// Every division of <paramref name="arc"/> touched between sunrise and next sunrise.
    /// Yields 1–3 spans: 1 when a long element covers the whole day (vriddhi), 3 when a
    /// short one is wholly contained (kshaya). Both occur regularly, not rarely.
    /// </summary>
    static List<Span> Spans(double from, double to, Func<double, double> f, double arc, int count)
    {
        var result = new List<Span>();
        double cursor = from;
        int guard = 0;

        while (cursor < to && guard++ < 6)
        {
            double value = f(cursor);
            int idx = (int)(value / arc);
            if (idx >= count) idx = count - 1;             // guards the exact-360.0 case

            double target = (idx + 1) * arc;              // next boundary, in [0,360]
            double end = SolveCrossing(f, cursor, target, arc);
            result.Add(new Span(idx, cursor, end));
            cursor = end + 1.0 / 1440.0;                  // step a minute past the boundary
        }

        if (result.Count == 0) result.Add(new Span((int)(f(from) / arc), from, to));
        return result;
    }

    /// <summary>
    /// Instant at which f(t) reaches <paramref name="target"/> degrees, by bisection on the
    /// unwrapped difference. Robust across the 360→0 wrap, which is where naive solvers fail.
    /// </summary>
    static double SolveCrossing(Func<double, double> f, double start, double target, double arc)
    {
        double Diff(double t)
        {
            double d = target - f(t);
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return d;
        }

        double lo = start, hi = start + 2.0;              // an element never lasts 2 days
        double flo = Diff(lo);
        if (flo <= 0) return start;

        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2.0;
            if (Diff(mid) > 0) lo = mid; else hi = mid;
            if (hi - lo < 1e-7) break;                     // ≈ 0.01 s
        }
        return (lo + hi) / 2.0;
    }

    static string TithiName(int idx0) =>
        idx0 == 29 ? "Amavasya" : Tithis[idx0 % 15];

    /// <summary>
    /// "Dasami upto 13:58" — or "upto 01:41, 27 Jul" when the element ends after midnight,
    /// so the reader sees the true wall-clock time and the day it actually falls on.
    /// </summary>
    static string FormatSpans(List<Span> spans, Func<int, string> name, double dayStart)
    {
        var parts = new List<string>();
        for (int i = 0; i < spans.Count; i++)
        {
            var s = spans[i];
            parts.Add(i == spans.Count - 1
                ? $"{name(s.Index)} full day"
                : $"{name(s.Index)} upto {Extended(s.End, dayStart)}");
        }
        // the final span has no end within the day; label it as continuing
        if (spans.Count > 1)
            parts[^1] = $"{name(spans[^1].Index)} from {Extended(spans[^1].Start, dayStart)}";
        return string.Join(", ", parts);
    }

    // ---------- kalam helpers ----------

    static string Eighth(double start, double length, int part) =>
        Range(start + (part - 1) * length / 8.0, start + part * length / 8.0, start);

    static string Muhurta(double start, double length, int index) =>
        Range(start + (index - 1) * length / 15.0, start + index * length / 15.0, start);

    /// <summary>
    /// Varjyam / Amrutha Kalam windows. Each is a fixed fraction of the CURRENT nakshatra's
    /// full span, so a day with two nakshatras can legitimately carry two windows.
    /// </summary>
    static string NakshatraWindows(List<Span> nakSpans, double dayStart, Func<int, int[]> ghatikas)
    {
        var windows = new List<string>();
        foreach (var span in nakSpans)
        {
            // the nakshatra's total length, not just the part inside this day
            double full = FullNakshatraLength(span);
            foreach (int g in ghatikas(span.Index))
            {
                double s = span.Start - (span.Start - NakshatraStart(span)) + g / (double)GhatikasPerNakshatra * full;
                double e = s + WindowGhatikas / (double)GhatikasPerNakshatra * full;
                if (e <= dayStart || s >= dayStart + 1.05) continue;       // not visible today
                windows.Add(Range(s, e, dayStart));
            }
        }
        return windows.Count == 0 ? "None" : string.Join(", ", windows);
    }

    static double NakshatraStart(Span span)
    {
        // walk back to where this nakshatra actually began
        double t = span.Start;
        double target = span.Index * NakshatraArc;
        double lo = t - 1.5, hi = t;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2.0;
            double d = target - MoonLongitude(mid);
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            if (d > 0) lo = mid; else hi = mid;
            if (hi - lo < 1e-7) break;
        }
        return (lo + hi) / 2.0;
    }

    static double FullNakshatraLength(Span span)
    {
        double start = NakshatraStart(span);
        double end = SolveCrossing(MoonLongitude, start + 1e-4, (span.Index + 1) * NakshatraArc, NakshatraArc);
        return end - start;
    }

    // ---------- calendar context ----------

    /// <summary>
    /// Amanta month: named by the rashi the Sun enters during the lunation. A lunation with
    /// no ingress is Adhika (intercalary) — 2026 has one, Adhika Jyeshtham from 17 May.
    /// </summary>
    static (int MasamIndex, bool IsAdhika) ResolveMasam(double jd)
    {
        double thisAmavasya = PreviousAmavasya(jd);
        double nextAmavasya = NextAmavasya(jd);

        int rashiAt(double t) => (int)(Astro.SunSidereal(t) / 30.0);
        int rStart = rashiAt(thisAmavasya + 1e-4);
        int rEnd = rashiAt(nextAmavasya - 1e-4);

        if (rStart == rEnd)
        {
            // no solar ingress inside this lunation -> Adhika, named for the coming month
            return (((rEnd + 1) % 12), true);
        }
        // month is named by the rashi entered during the lunation
        return ((rEnd % 12), false);
    }

    /// <summary>
    /// The last instant of zero elongation (Amavasya end) strictly BEFORE <paramref name="jd"/>.
    /// The "strictly before" guard matters: on a day whose Amavasya ends shortly after sunrise,
    /// the day still belongs to the outgoing month.
    /// </summary>
    static double PreviousAmavasya(double jd)
    {
        double result = SolveAmavasyaNear(jd);
        int guard = 0;
        while (result >= jd && guard++ < 3)
            result = SolveAmavasyaNear(result - 29.53 / 2.0);
        return result;
    }

    /// <summary>Bisects the elongation 360→0 wrap inside the lunation containing <paramref name="seed"/>.</summary>
    static double SolveAmavasyaNear(double seed)
    {
        // walk back until elongation is descending through the wrap point
        double t = seed;
        for (int i = 0; i < 800; i++)
        {
            double e = Elongation(t);
            double ePrev = Elongation(t - 0.05);
            if (e < ePrev && ePrev > 300 && e < 60) break;      // wrap sits between t-0.05 and t
            if (e < 60 && ePrev > 300) break;
            t -= 0.05;
        }
        double lo = t - 0.05, hi = t;
        for (int i = 0; i < 80; i++)
        {
            double mid = (lo + hi) / 2.0;
            if (Elongation(mid) > 180) lo = mid; else hi = mid;
            if (hi - lo < 1e-7) break;
        }
        return (lo + hi) / 2.0;
    }

    static double NextAmavasya(double jd) => PreviousAmavasya(jd + 29.53);

    /// <summary>Shaka year: increments at the Chaitra Shukla Padyami (Ugadi) new year.</summary>
    static int ResolveSakaYear(double jd)
    {
        var (y, m, d, _) = Astro.FromJulianDay(jd + IstOffsetHours / 24.0);
        int saka = y - 78;
        // before Ugadi (which falls in Mar/Apr) the Shaka year is still the previous one
        double ugadi = UgadiJd(y);
        if (jd < ugadi) saka -= 1;
        return saka;
    }

    /// <summary>Ugadi = the sunrise-day following the Amavasya that ends Phalgunam.</summary>
    static double UgadiJd(int gregorianYear)
    {
        // search the amavasya closest to mid-March
        double seed = Astro.JulianDay(gregorianYear, 3, 20.0) - IstOffsetHours / 24.0;
        double a = PreviousAmavasya(seed + 15);
        if (a > seed + 20) a = PreviousAmavasya(seed);
        return Math.Floor(a + IstOffsetHours / 24.0) + 0.5 - IstOffsetHours / 24.0;
    }

    /// <summary>Uttarayanam from the Makara sankranti, Dakshinayanam from Karka.</summary>
    static string ResolveAyanam(double jd)
    {
        double lon = Astro.SunSidereal(jd);
        // sidereal Capricorn (270°) through Gemini (<90°) is Uttarayanam
        return (lon >= 270.0 || lon < 90.0) ? "Uttarayanam" : "Dakshinayanam";
    }

    // ---------- formatting ----------

    static TimeOnly ToTime(double jdUt)
    {
        var (_, _, _, h) = Astro.FromJulianDay(jdUt + IstOffsetHours / 24.0);
        int hh = (int)h;
        int mm = (int)Math.Round((h - hh) * 60.0);
        if (mm == 60) { mm = 0; hh++; }
        if (hh >= 24) hh -= 24;
        return new TimeOnly(hh, mm);
    }

    /// <summary>
    /// Clock time (HH:mm). When the instant falls on a later civil day than the panchang day —
    /// e.g. an Amrutha Kalam or tithi end that runs past midnight — it is tagged with that day's
    /// date ("03:18, 27 Jul"). This matches how mainstream panchangams (DrikPanchang etc.) print
    /// after-midnight windows, rather than a 24+ hour value like "27:18".
    /// </summary>
    static string Extended(double jdUt, double dayStart)
    {
        var when = LocalRounded(jdUt);
        var (sy, sm, sd, _) = Astro.FromJulianDay(dayStart + IstOffsetHours / 24.0);
        int dayOffset = (when.Date - new DateTime(sy, sm, sd)).Days;
        return dayOffset >= 1 ? $"{when:HH\\:mm}, {when:dd MMM}" : when.ToString("HH\\:mm");
    }

    /// <summary>Local (IST) instant rounded to the nearest minute, carrying across midnight.</summary>
    static DateTime LocalRounded(double jdUt)
    {
        var (y, mo, d, h) = Astro.FromJulianDay(jdUt + IstOffsetHours / 24.0);
        var t = new DateTime(y, mo, d).AddHours(h).AddSeconds(30);
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
    }

    static string Range(double a, double b, double dayStart) =>
        $"{Extended(a, dayStart)} – {Extended(b, dayStart)}";
}

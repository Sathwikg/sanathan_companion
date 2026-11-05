using Sanathana.Companion.Application.HinduCalendar;
using Sanathana.Companion.Application.Panchangam;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Pins the rules the calendar grid is drawn from: how the Panchangam's text is read back into a
/// tithi, which observances a tithi carries, how the moon is drawn, and when a day reads as good,
/// neutral or caution.
/// </summary>
public class CalendarRulesTests
{
    // ---------------------------------------------------------------- reading the text

    [Theory]
    [InlineData("Dasami upto 13:58, Ekadasi from 13:58", "Shukla", 9)]
    [InlineData("Pournami full day", "Shukla", 14)]
    [InlineData("Padyami upto 06:12, Vidiya upto 04:30, 27 Jul, Tadiya from 04:30, 27 Jul", "Krishna (Bahula)", 15)]
    [InlineData("Amavasya upto 11:02, Padyami from 11:02", "Krishna (Bahula)", 29)]
    [InlineData("Ekadasi full day", "Krishna (Bahula)", 25)]
    public void The_tithi_at_sunrise_is_read_from_the_calculator_text(string details, string paksham, int expected)
    {
        Assert.True(CalendarRules.TryTithiIndex(details, paksham, out var index));
        Assert.Equal(expected, index);
    }

    [Theory]
    [InlineData(null, "Shukla")]
    [InlineData("", "Shukla")]
    [InlineData("Not a tithi upto 10:00", "Shukla")]
    [InlineData("Pournami full day", "Krishna (Bahula)")]   // never emitted: 29 is written Amavasya
    public void Text_that_is_not_the_calculators_is_refused(string? details, string paksham)
        => Assert.False(CalendarRules.TryTithiIndex(details, paksham, out _));

    [Fact]
    public void Every_real_day_of_a_year_reads_back_to_the_tithi_and_nakshatra_it_was_computed_from()
    {
        // Hyderabad, as the compute tests use. Whatever the calculator writes, the rules must read.
        for (var d = new DateOnly(2026, 1, 1); d.Year == 2026; d = d.AddDays(1))
        {
            var day = PanchangamCalculator.Compute(d, 17.39, 78.49);

            Assert.True(CalendarRules.TryTithiIndex(day.TithiFormatted, day.Paksham, out var t), $"{d}: {day.TithiFormatted}");
            Assert.Equal(day.Paksham.StartsWith("Shukla"), t < 15);
            Assert.StartsWith(CalendarRules.TithiName(t), day.TithiFormatted);

            Assert.True(CalendarRules.TryNakshatraIndex(day.NakshatramFormatted, out var n), $"{d}: {day.NakshatramFormatted}");
            Assert.StartsWith(PanchangamTables.Nakshatras[n], day.NakshatramFormatted);
        }
    }

    [Fact]
    public void A_year_has_about_twelve_full_moons_and_twelve_new_moons()
    {
        int full = 0, @new = 0;
        for (var d = new DateOnly(2026, 1, 1); d.Year == 2026; d = d.AddDays(1))
        {
            var day = PanchangamCalculator.Compute(d, 17.39, 78.49);
            CalendarRules.TryTithiIndex(day.TithiFormatted, day.Paksham, out var t);
            if (t == CalendarRules.Pournami) full++;
            if (t == CalendarRules.Amavasya) @new++;
        }
        // A tithi can span two sunrises (vriddhi) or none (kshaya), so 11..13 of each.
        Assert.InRange(full, 11, 13);
        Assert.InRange(@new, 11, 13);
    }

    // ---------------------------------------------------------------- the moon

    [Theory]
    [InlineData(29, "new", 0)]
    [InlineData(0, "new", 0)]
    [InlineData(7, "firstQuarter", 50)]
    [InlineData(14, "full", 100)]
    [InlineData(22, "lastQuarter", 50)]
    public void The_moon_phase_and_illumination_follow_the_tithi(int tithi, string phase, int illumination)
    {
        Assert.Equal(phase, CalendarRules.MoonPhase(tithi));
        Assert.InRange(CalendarRules.MoonIllumination(tithi), illumination - 3, illumination + 3);
    }

    // ---------------------------------------------------------------- observances

    [Theory]
    [InlineData(14, "purnima")]
    [InlineData(29, "amavasya")]
    [InlineData(10, "ekadashi")]
    [InlineData(25, "ekadashi")]
    [InlineData(12, "pradosham")]
    [InlineData(27, "pradosham")]
    [InlineData(3, "vinayakaChaturthi")]
    [InlineData(18, "sankashtiChaturthi")]
    [InlineData(28, "masaShivaratri")]
    public void The_monthly_observances_sit_on_their_tithis(int tithi, string marker)
        => Assert.Contains(marker, CalendarRules.Markers(tithi));

    [Fact]
    public void An_ordinary_tithi_carries_no_observance()
        => Assert.Empty(CalendarRules.Markers(1));

    // ---------------------------------------------------------------- the reading

    [Fact]
    public void Pournami_on_a_Thursday_under_Pushyami_reads_good()
    {
        var a = CalendarRules.Assess(CalendarRules.Pournami, 7, DayOfWeek.Thursday, hasFestival: false);
        Assert.Equal(DayRating.Good, a.Rating);
        Assert.Contains("purnima", a.Reasons);
        Assert.Contains("nakshatraAuspicious", a.Reasons);
        Assert.Contains("weekdayAuspicious", a.Reasons);
    }

    [Fact]
    public void A_rikta_tithi_under_a_harsh_star_on_a_Saturday_reads_caution()
    {
        // Chaviti (3) + Bharani (1) + Saturday
        var a = CalendarRules.Assess(3, 1, DayOfWeek.Saturday, hasFestival: false);
        Assert.Equal(DayRating.Caution, a.Rating);
        Assert.Contains("tithiRikta", a.Reasons);
        Assert.Contains("nakshatraHarsh", a.Reasons);
        Assert.Contains("weekdayHarsh", a.Reasons);
    }

    [Fact]
    public void A_festival_lifts_an_otherwise_middling_day()
    {
        // Padyami (0) + Satabhisham (23, neutral) on a Monday: neutral alone, good with a festival.
        Assert.Equal(DayRating.Neutral, CalendarRules.Assess(0, 23, DayOfWeek.Monday, hasFestival: false).Rating);
        var withFestival = CalendarRules.Assess(1, 23, DayOfWeek.Monday, hasFestival: true);
        Assert.Equal(DayRating.Good, withFestival.Rating);
        Assert.Contains("festival", withFestival.Reasons);
    }

    [Fact]
    public void A_festival_day_is_never_read_as_caution()
    {
        // Chaviti (rikta) + Bharani (harsh) + Saturday is caution on its own; a festival lifts it to
        // neutral and keeps the honest score and reasons.
        var plain = CalendarRules.Assess(3, 1, DayOfWeek.Saturday, hasFestival: false);
        var festival = CalendarRules.Assess(3, 1, DayOfWeek.Saturday, hasFestival: true);
        Assert.Equal(DayRating.Caution, plain.Rating);
        Assert.Equal(DayRating.Neutral, festival.Rating);
        Assert.Equal(plain.Score + 1, festival.Score);
        Assert.Contains("tithiRikta", festival.Reasons);
    }

    [Fact]
    public void A_day_without_a_Panchangam_is_still_read_from_its_weekday_and_festivals()
    {
        var a = CalendarRules.Assess(null, null, DayOfWeek.Tuesday, hasFestival: false);
        Assert.Equal(DayRating.Neutral, a.Rating);
        Assert.Equal(new[] { "weekdayHarsh" }, a.Reasons);
    }

    [Fact]
    public void Amavasya_is_never_marked_good_without_a_festival()
    {
        foreach (var nak in Enumerable.Range(0, 27))
            foreach (DayOfWeek dow in Enum.GetValues<DayOfWeek>())
                Assert.NotEqual(DayRating.Good, CalendarRules.Assess(CalendarRules.Amavasya, nak, dow, hasFestival: false).Rating);
    }
}

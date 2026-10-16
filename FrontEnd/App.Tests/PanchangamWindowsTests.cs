using App.Core.Models;
using App.Core.Services;

namespace App.Tests;

/// <summary>
/// The window text is the server's contract; these pin what the phone home reads out of it, with
/// values taken from the calculator's own golden output (panchangam-2026-sample.json).
/// </summary>
public class PanchangamWindowsTests
{
    private static readonly DateOnly Day = new(2026, 10, 15);

    private static DateTime At(string hhmm, int dayOffset = 0) => Day.AddDays(dayOffset).ToDateTime(TimeOnly.Parse(hhmm));

    private static PanchangamModel Row(DateOnly date, string? rahu = null, string? yama = null, string? gulika = null,
        string? durmu = null, string? varjyam = null, string? amrutha = null, string? abhijit = null,
        string? tithi = null, string? nakshatram = null, TimeOnly? sunrise = null) => new()
    {
        Date = date, RahuKalam = rahu, Yamagandam = yama, Gulika = gulika, Durmuhurtham = durmu,
        Varjyam = varjyam, AmruthaKalam = amrutha, AbhijitMuhurtham = abhijit,
        TithiDetails = tithi, NakshatramDetails = nakshatram, Sunrise = sunrise ?? new TimeOnly(6, 6),
    };

    [Fact]
    public void A_single_range_is_one_window_on_the_rows_day()
    {
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.RahuKalam, "13:36 – 15:05", Day));
        Assert.Equal(At("13:36"), w.Start);
        Assert.Equal(At("15:05"), w.End);
    }

    [Fact]
    public void Ranges_joined_with_a_comma_are_separate_windows_and_the_comma_is_not_a_day_suffix()
    {
        var ws = PanchangamWindows.Parse(PanchangamWindowKind.Durmuhurtham, "10:06 – 10:54, 14:53 – 15:41", Day);
        Assert.Equal(2, ws.Count);
        Assert.Equal(At("14:53"), ws[1].Start);
        Assert.Equal(At("15:41"), ws[1].End);
    }

    [Theory]
    [InlineData("23:12 – 03:46, 16 Oct")]
    [InlineData("23:12 – 03:46, 16 Sept")]
    [InlineData("23:12 – 03:46, 16 [అక్టోబర్]")]
    public void A_day_suffix_moves_the_instant_to_that_later_day_whatever_the_month_token_says(string text)
    {
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.Varjyam, text, Day));
        Assert.Equal(At("23:12"), w.Start);
        Assert.Equal(At("03:46", 1), w.End);
    }

    [Fact]
    public void A_window_wholly_after_midnight_lands_on_the_next_day()
    {
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.AmruthaKalam, "01:28, 16 Oct – 02:57, 16 Oct", Day));
        Assert.Equal(At("01:28", 1), w.Start);
        Assert.Equal(At("02:57", 1), w.End);
    }

    [Fact]
    public void A_window_that_straddles_sunrise_stays_on_its_own_day()
    {
        // Printed without a suffix because both instants are on the row's date, even though 05:30 is before sunrise.
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.AmruthaKalam, "05:30 – 07:06", Day));
        Assert.Equal(At("05:30"), w.Start);
    }

    [Fact]
    public void A_year_boundary_suffix_lands_in_January_not_a_year_back()
    {
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.Varjyam, "22:49 – 00:22, 01 Jan", new DateOnly(2026, 12, 31)));
        Assert.Equal(new DateTime(2027, 1, 1, 0, 22, 0), w.End);
    }

    [Fact]
    public void An_undated_end_before_its_start_rolls_to_the_next_day()
    {
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.Durmuhurtham, "23:50 – 01:20", Day));
        Assert.Equal(At("01:20", 1), w.End);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("None")]
    [InlineData("లేదు")]
    [InlineData("कोई नहीं")]
    [InlineData("இல்லை")]
    [InlineData("ಇಲ್ಲ")]
    public void No_window_in_any_language_is_an_empty_list(string? text)
        => Assert.Empty(PanchangamWindows.Parse(PanchangamWindowKind.AbhijitMuhurtham, text, Day));

    [Fact]
    public void A_trailing_unpaired_time_is_dropped()
        => Assert.Empty(PanchangamWindows.Parse(PanchangamWindowKind.Gulika, "07:30", Day));

    [Fact]
    public void Translated_words_around_the_times_change_nothing()
    {
        var w = Assert.Single(PanchangamWindows.Parse(PanchangamWindowKind.Varjyam, "23:41, 16 [జూలై] – 01:29, 17 [జూలై]", Day));
        Assert.Equal(At("23:41", 1), w.Start);
        Assert.Equal(At("01:29", 2), w.End);
    }

    [Fact]
    public void ParseRow_collects_all_seven_fields_with_their_kinds()
    {
        var row = Row(Day, rahu: "13:36 – 15:05", yama: "06:06 – 07:36", gulika: "09:06 – 10:36",
            durmu: "10:06 – 10:54, 14:53 – 15:41", varjyam: "20:59 – 22:29", amrutha: "01:28, 16 Oct – 02:57, 16 Oct",
            abhijit: "11:42 – 12:30");
        var ws = PanchangamWindows.ParseRow(row);
        Assert.Equal(8, ws.Count);
        Assert.Equal(Enum.GetValues<PanchangamWindowKind>().Length, ws.Select(w => w.Kind).Distinct().Count());
    }

    [Theory]
    [InlineData(PanchangamWindowKind.RahuKalam, PanchangamTone.Red)]
    [InlineData(PanchangamWindowKind.Yamagandam, PanchangamTone.Amber)]
    [InlineData(PanchangamWindowKind.Gulika, PanchangamTone.Amber)]
    [InlineData(PanchangamWindowKind.Durmuhurtham, PanchangamTone.Amber)]
    [InlineData(PanchangamWindowKind.Varjyam, PanchangamTone.Amber)]
    [InlineData(PanchangamWindowKind.AmruthaKalam, PanchangamTone.Green)]
    [InlineData(PanchangamWindowKind.AbhijitMuhurtham, PanchangamTone.Green)]
    public void Each_kind_has_its_tier(PanchangamWindowKind kind, PanchangamTone tone)
        => Assert.Equal(tone, PanchangamWindows.ToneOf(kind));

    /// <summary>Yesterday, today and tomorrow of a Thursday in Telangana, in the calculator's own words.</summary>
    private static List<PanchangamModel> Thursday() => new()
    {
        Row(Day.AddDays(-1), varjyam: "22:40 – 00:10, 15 Oct", amrutha: "19:58 – 21:23",
            tithi: "Chaturthi upto 14:10, Panchami from 14:11",
            nakshatram: "Krittika upto 02:10, 15 Oct, Rohini from 02:11, 15 Oct"),
        Row(Day, rahu: "13:36 – 15:05", yama: "06:06 – 07:36", gulika: "09:06 – 10:36",
            durmu: "10:06 – 10:54, 14:53 – 15:41", varjyam: "20:59 – 22:29", amrutha: "01:28, 16 Oct – 02:57, 16 Oct",
            abhijit: "11:42 – 12:30",
            tithi: "Panchami upto 12:36, Shashti from 12:37",
            nakshatram: "Rohini upto 04:27, 16 Oct, Mrigasira from 04:28, 16 Oct"),
        Row(Day.AddDays(1), yama: "06:06 – 07:36", rahu: "10:36 – 12:06",
            tithi: "Shashti upto 10:02, Saptami from 10:03",
            nakshatram: "Mrigasira upto 06:05, Arudra from 06:06"),
    };

    [Fact]
    public void Red_beats_amber_and_the_amber_window_becomes_secondary()
    {
        var s = PanchangamWindows.Evaluate(Thursday(), At("15:00"));
        Assert.Equal(PanchangamWindowKind.RahuKalam, s.Active!.Kind);
        Assert.Equal(PanchangamWindowKind.Durmuhurtham, s.Secondary!.Kind);
        Assert.Equal(PanchangamTone.Red, s.Tone);
        Assert.Equal(At("15:05"), s.NextChange);
    }

    [Fact]
    public void Amber_beats_green_when_they_overlap()
    {
        var rows = new List<PanchangamModel> { Row(Day, yama: "12:00 – 13:26", abhijit: "11:37 – 12:23") };
        var s = PanchangamWindows.Evaluate(rows, At("12:10"));
        Assert.Equal(PanchangamWindowKind.Yamagandam, s.Active!.Kind);
        Assert.Equal(PanchangamWindowKind.AbhijitMuhurtham, s.Secondary!.Kind);
    }

    [Fact]
    public void Same_tone_the_window_ending_first_leads()
    {
        var rows = new List<PanchangamModel> { Row(Day, gulika: "15:20 – 16:46", durmu: "16:40 – 17:26") };
        var s = PanchangamWindows.Evaluate(rows, At("16:42"));
        Assert.Equal(PanchangamWindowKind.Gulika, s.Active!.Kind);
        Assert.Equal(PanchangamWindowKind.Durmuhurtham, s.Secondary!.Kind);
    }

    [Fact]
    public void The_start_is_inclusive_and_the_end_exclusive()
    {
        var rows = Thursday();
        Assert.Equal(PanchangamWindowKind.RahuKalam, PanchangamWindows.Evaluate(rows, At("13:36")).Active!.Kind);
        Assert.NotEqual(PanchangamWindowKind.RahuKalam, PanchangamWindows.Evaluate(rows, At("15:05")).Active?.Kind);
    }

    [Fact]
    public void When_nothing_is_active_the_next_window_is_reported()
    {
        var s = PanchangamWindows.Evaluate(Thursday(), At("18:30"));
        Assert.Null(s.Active);
        Assert.Equal(PanchangamWindowKind.Varjyam, s.Next!.Kind);
        Assert.Equal(At("20:59"), s.Next.Start);
        Assert.Equal(At("20:59"), s.NextChange);
    }

    [Fact]
    public void After_the_last_window_tomorrows_row_supplies_the_next_one()
    {
        // 03:30 on the 16th: the Amrutha from the 15th's row has ended; the next window is the 16th's own Yamagandam.
        var s = PanchangamWindows.Evaluate(Thursday(), At("03:30", 1));
        Assert.Null(s.Active);
        Assert.Equal(PanchangamWindowKind.Yamagandam, s.Next!.Kind);
        Assert.Equal(At("06:06", 1), s.Next.Start);
    }

    [Fact]
    public void Yesterdays_spill_past_midnight_is_active_in_the_small_hours()
    {
        var s = PanchangamWindows.Evaluate(Thursday(), At("00:05"));
        Assert.Equal(PanchangamWindowKind.Varjyam, s.Active!.Kind);
        Assert.False(s.Unknown);
    }

    [Fact]
    public void Without_yesterdays_row_the_small_hours_are_unknown_rather_than_clear()
    {
        var rows = Thursday().Where(r => r.Date != Day.AddDays(-1)).ToList();
        var s = PanchangamWindows.Evaluate(rows, At("00:05"));
        Assert.True(s.Unknown);
        Assert.True(s.MissingYesterday);
    }

    [Fact]
    public void Without_todays_row_there_is_nothing_to_say()
        => Assert.Same(PanchangamSnapshot.Empty, PanchangamWindows.Evaluate(new[] { Row(Day.AddDays(-1)) }, At("12:00")));

    [Fact]
    public void A_straddling_window_printed_by_two_rows_counts_once()
    {
        var rows = new List<PanchangamModel>
        {
            Row(Day.AddDays(-1), amrutha: "06:18, 15 Oct – 07:52, 15 Oct"),
            Row(Day, amrutha: "06:18 – 07:53"),
        };
        Assert.Single(PanchangamWindows.AllWindows(rows));
    }

    [Fact]
    public void Segments_read_upto_and_from_without_matching_the_words()
    {
        var spans = PanchangamWindows.Segments("Panchami upto 12:36, Shashti from 12:37", Day);
        Assert.Equal(2, spans.Count);
        Assert.Equal("Panchami upto 12:36", spans[0].Text);
        Assert.Equal(At("12:36"), spans[0].End);
        Assert.Equal("Shashti from 12:37", spans[1].Text);
        Assert.Equal(At("12:36"), spans[1].Start);   // contiguous: no minute without a span
        Assert.Null(spans[1].End);
    }

    [Fact]
    public void The_minute_a_tithi_changes_belongs_to_the_new_one()
    {
        var rows = Thursday();
        Assert.Equal("Shashti upto 10:02", PanchangamWindows.Current(rows, r => r.TithiDetails, At("12:36"))!.Text);
        Assert.Equal("Panchami upto 12:36", PanchangamWindows.Current(rows, r => r.TithiDetails, At("12:35"))!.Text);
    }

    [Fact]
    public void A_full_day_segment_has_no_instant()
    {
        var span = Assert.Single(PanchangamWindows.Segments("Vidiya full day", Day));
        Assert.Null(span.Start);
        Assert.Null(span.End);
    }

    [Fact]
    public void The_current_tithi_is_read_across_the_row_boundary()
    {
        var rows = Thursday();
        var afternoon = PanchangamWindows.Current(rows, r => r.TithiDetails, At("15:00"));
        Assert.Equal("Shashti upto 10:02", afternoon!.Text);   // joined with tomorrow's first span, which carries the end
        Assert.Equal(At("10:02", 1), afternoon.End);

        var smallHours = PanchangamWindows.Current(rows, r => r.NakshatramDetails, At("01:00"));
        Assert.Equal("Krittika upto 02:10, 15 Oct", smallHours!.Text);   // from yesterday's row
    }

    [Fact]
    public void Compact_text_is_twelve_hour_without_the_day_marker()
    {
        Assert.Equal("Rohini upto 4:27\u00A0AM", PanchangamWindows.Compact("Rohini upto 04:27, 16 Oct"));
        Assert.Equal("Shashti from 12:37\u00A0PM", PanchangamWindows.Compact("Shashti from 12:37"));
        Assert.Equal("Vidiya full day", PanchangamWindows.Compact("Vidiya full day"));
    }
}
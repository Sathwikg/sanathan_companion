using System.Globalization;
using App.Core.Services;

namespace App.Tests;

public class ClockTextTests
{
    [Theory]
    [InlineData("07:27 – 08:58", "7:27 AM – 8:58 AM")]
    [InlineData("Vidiya upto 19:14", "Vidiya upto 7:14 PM")]
    [InlineData("00:05", "12:05 AM")]
    [InlineData("12:00", "12:00 PM")]
    [InlineData("upto 01:41, 27 Jul", "upto 1:41 AM, 27 Jul")]
    [InlineData("త్రయోదశి వరకు 22:23", "త్రయోదశి వరకు 10:23 PM")]
    public void Every_clock_time_in_a_value_is_rewritten_with_a_plain_space(string input, string expected)
        => Assert.Equal(expected, ClockText.To12Hour(input, " "));

    [Fact]
    public void On_screen_the_time_and_meridiem_are_joined_by_a_no_break_space()
        => Assert.Equal("7:14\u00A0PM", ClockText.To12Hour("19:14"));

    [Theory]
    [InlineData("10:30:15")]
    [InlineData("24:00")]
    [InlineData("123:45")]
    [InlineData("1:2")]
    [InlineData("no time here")]
    public void Text_without_a_clock_time_is_untouched(string input)
        => Assert.Equal(input, ClockText.To12Hour(input));

    [Fact]
    public void Rewriting_twice_gives_the_same_result()
    {
        var once = ClockText.To12Hour("13:43 – 15:06", " ");
        Assert.Equal(once, ClockText.To12Hour(once, " "));
    }

    [Fact]
    public void Indic_digits_are_not_mistaken_for_a_clock_time()
        => Assert.Equal("౧౯:౧౪", ClockText.To12Hour("౧౯:౧౪"));

    [Fact]
    public void The_device_culture_never_leaks_into_the_output()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ta-IN");
            Assert.Equal("7:14\u00A0PM", ClockText.Time(new TimeOnly(19, 14)));
            Assert.Equal("Thu, 15 Oct 2026", ClockText.ShortDate(new DateOnly(2026, 10, 15)));
            Assert.Equal("Thu, 15 Oct", ClockText.DayMonth(new DateOnly(2026, 10, 15)));
            Assert.Equal("Thursday, 15 October 2026", ClockText.LongDate(new DateOnly(2026, 10, 15)));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void A_range_cannot_break_across_lines()
    {
        var range = ClockText.Range(new DateTime(2026, 10, 15, 13, 36, 0), new DateTime(2026, 10, 15, 15, 5, 0));
        Assert.Equal("1:36\u00A0PM\u00A0–\u2060\u00A03:05\u00A0PM", range);
        Assert.DoesNotContain(' ', range);
    }
}
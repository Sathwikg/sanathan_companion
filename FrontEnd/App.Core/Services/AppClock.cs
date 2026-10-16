namespace App.Core.Services;

/// <summary>
/// The wall clock for everything the server reckons in India Standard Time: the Panchangam windows
/// (computed in IST), the Prayers "Now" badge and the sadhana streak's "today" (both
/// DateTime.UtcNow + 5:30 on the server). Two "Now" badges on one screen must agree, so the phone
/// home evaluates against this clock rather than the device's. For a phone in India it is the
/// wall clock; abroad it is the clock the Panchangam itself is written in.
/// </summary>
public static class AppClock
{
    public static readonly TimeSpan IstOffset = new(5, 30, 0);

    /// <summary>Kind Unspecified on purpose: an IST wall-clock value must never be mistaken for UTC.</summary>
    public static DateTime ToIst(DateTime utc) => DateTime.SpecifyKind(utc + IstOffset, DateTimeKind.Unspecified);

    public static DateTime NowIst => ToIst(DateTime.UtcNow);

    public static DateOnly TodayIst => DateOnly.FromDateTime(NowIst);

    /// <summary>Time from <paramref name="now"/> to <paramref name="when"/>, never under one second — what a one-shot timer is armed with.</summary>
    public static TimeSpan Until(DateTime now, DateTime when)
    {
        var d = when - now;
        return d < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : d;
    }
}
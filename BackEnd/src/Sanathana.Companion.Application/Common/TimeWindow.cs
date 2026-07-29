namespace Sanathana.Companion.Application.Common;

/// <summary>Daily time windows that may wrap past midnight (e.g. quiet hours 22:00 → 06:00).</summary>
public static class TimeWindow
{
    /// <summary>
    /// True when <paramref name="now"/> falls inside [from, to]. A window whose end is before its
    /// start is treated as spanning midnight. An incomplete window (either side null) contains nothing.
    /// </summary>
    public static bool Contains(TimeOnly? from, TimeOnly? to, TimeOnly now)
    {
        if (from is not { } f || to is not { } t) return false;
        return f <= t ? now >= f && now <= t : now >= f || now <= t;
    }
}

namespace App.Core.Services;

/// <summary>
/// Coarsens a device fix before it leaves the app.
/// </summary>
/// <remarks>
/// A Panchangam needs to know roughly where the sun rises for you, not which room you are in.
/// Longitude maps to local solar time at four minutes per degree, so two decimal places is about
/// 2.4 seconds of solar time — and the calculator rounds its answers to the whole minute anyway.
/// At a cell boundary a printed sunrise can still move by one minute; that is the accepted cost of
/// not handing a street address to every hop between the phone and the database.
/// </remarks>
public static class GeoPrecision
{
    public static double Round(double degrees) => Math.Round(degrees, 2, MidpointRounding.AwayFromZero);
}

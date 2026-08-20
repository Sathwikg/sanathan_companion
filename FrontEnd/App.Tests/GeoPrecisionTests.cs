using App.Core.Services;

namespace App.Tests;

/// <summary>
/// A Panchangam needs to know roughly where the sun rises for you, not which room you are in.
/// These pin how much of the device fix actually leaves the phone.
/// </summary>
public class GeoPrecisionTests
{
    [Theory]
    [InlineData(17.385044, 17.39)]
    [InlineData(78.486671, 78.49)]
    [InlineData(-78.486671, -78.49)]      // southern and western hemispheres round the same way
    [InlineData(0.0, 0.0)]
    [InlineData(17.0, 17.0)]              // an already-coarse value is left alone
    public void A_fix_is_coarsened_to_two_decimal_places(double raw, double sent)
        => Assert.Equal(sent, GeoPrecision.Round(raw));

    [Theory]
    // 0.125 and 0.375, because those are exact in binary. The obvious choice, 1.005, is really
    // 1.0049999999999999 as a double and rounds down whichever midpoint rule is in force, so it
    // would prove nothing about the one that was chosen.
    [InlineData(0.125, 0.13)]
    [InlineData(-0.125, -0.13)]
    [InlineData(0.375, 0.38)]
    public void An_exact_midpoint_rounds_away_from_zero_rather_than_to_even(double raw, double sent)
        => Assert.Equal(sent, GeoPrecision.Round(raw));

    [Fact]
    public void Two_decimals_is_about_a_kilometre_which_is_the_point()
    {
        // Roughly 1.1 km of latitude, versus the ~10 m a high-accuracy GPS fix would hand over.
        // Anything finer describes a building rather than a horizon.
        const double metresPerDegreeLatitude = 111_320;

        var here = 17.385044;
        var sent = GeoPrecision.Round(here);
        var displacementMetres = Math.Abs(here - sent) * metresPerDegreeLatitude;

        Assert.True(displacementMetres < 600, $"displaced by {displacementMetres:F0} m");
        Assert.True(0.01 * metresPerDegreeLatitude > 1_000);
    }
}

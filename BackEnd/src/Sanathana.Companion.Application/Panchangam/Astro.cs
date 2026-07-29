using System;

namespace Sanathana.Companion.Application.Panchangam;

/// <summary>
/// Sun and Moon positions and rise/set times, implemented from Jean Meeus,
/// "Astronomical Algorithms" (2nd ed.). No third-party ephemeris — the published
/// series are used directly, so there is no licensing encumbrance.
/// </summary>
public static class Astro
{
    const double Deg = Math.PI / 180.0;

    /// <summary>
    /// Normalise to [0, 360). The final guard is required, not cosmetic: for a tiny
    /// negative input such as -1e-14, (-1e-14 + 360.0) rounds to exactly 360.0, which
    /// would yield tithi index 31 / nakshatra index 28 and overrun the name tables.
    /// </summary>
    public static double Norm360(double d)
    {
        d %= 360.0;
        if (d < 0) d += 360.0;
        if (d >= 360.0) d = 0.0;
        return d;
    }
    static double Sin(double deg) => Math.Sin(deg * Deg);
    static double Cos(double deg) => Math.Cos(deg * Deg);

    /// <summary>Julian Day from a UTC calendar date (Gregorian). Meeus ch. 7.</summary>
    public static double JulianDay(int year, int month, double day)
    {
        if (month <= 2) { year -= 1; month += 12; }
        int a = year / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    /// <summary>Calendar date (UTC) from Julian Day. Meeus ch. 7.</summary>
    public static (int Year, int Month, int Day, double Hours) FromJulianDay(double jd)
    {
        double z = Math.Floor(jd + 0.5);
        double f = jd + 0.5 - z;
        double a = z;
        if (z >= 2299161)
        {
            double alpha = Math.Floor((z - 1867216.25) / 36524.25);
            a = z + 1 + alpha - Math.Floor(alpha / 4);
        }
        double b = a + 1524;
        double c = Math.Floor((b - 122.1) / 365.25);
        double d = Math.Floor(365.25 * c);
        double e = Math.Floor((b - d) / 30.6001);

        double dayFrac = b - d - Math.Floor(30.6001 * e) + f;
        int day = (int)Math.Floor(dayFrac);
        double hours = (dayFrac - day) * 24.0;
        int month = (int)(e < 14 ? e - 1 : e - 13);
        int year = (int)(month > 2 ? c - 4716 : c - 4715);
        return (year, month, day, hours);
    }

    /// <summary>Julian centuries from J2000.0.</summary>
    static double T(double jd) => (jd - 2451545.0) / 36525.0;

    /// <summary>
    /// Approximate ΔT (TT − UT) in seconds for the 2005–2050 window (Espenak/Meeus).
    /// Only ~70 s in this era; it matters for the Moon (≈0.6 arcmin) so it is applied.
    /// </summary>
    public static double DeltaTSeconds(double jd)
    {
        var (y, m, _, _) = FromJulianDay(jd);
        double year = y + (m - 0.5) / 12.0;
        double t = year - 2000.0;
        return 62.92 + 0.32217 * t + 0.005589 * t * t;
    }

    /// <summary>Nutation in longitude, arcseconds. Meeus ch. 22 (abridged).</summary>
    public static double NutationLongitude(double jde)
    {
        double t = T(jde);
        double omega = 125.04452 - 1934.136261 * t;
        double ls = 280.4665 + 36000.7698 * t;
        double lm = 218.3165 + 481267.8813 * t;
        return -17.20 * Sin(omega) - 1.32 * Sin(2 * ls) - 0.23 * Sin(2 * lm) + 0.21 * Sin(2 * omega);
    }

    // ---- Earth heliocentric longitude, truncated VSOP87D (Meeus Appendix III) ----
    // Each row: A, B, C  ->  A * cos(B + C * tau), tau = Julian millennia from J2000.
    static readonly double[,] EarthL0 = {
        {175347046,0,0},{3341656,4.6692568,6283.07585},{34894,4.6261,12566.1517},
        {3497,2.7441,5753.3849},{3418,2.8289,3.5231},{3136,3.6277,77713.7715},
        {2676,4.4181,7860.4194},{2343,6.1352,3930.2097},{1324,0.7425,11506.7698},
        {1273,2.0371,529.691},{1199,1.1096,1577.3435},{990,5.233,5884.927},
        {902,2.045,26.298},{857,3.508,398.149},{780,1.179,5223.694},
        {753,2.533,5507.553},{505,4.583,18849.228},{492,4.205,775.523},
        {357,2.92,0.067},{317,5.849,11790.629},{284,1.899,796.298},
        {271,0.315,10977.079},{243,0.345,5486.778},{206,4.806,2544.314},
        {205,1.869,5573.143},{202,2.458,6069.777},{156,0.833,213.299},
        {132,3.411,2942.463},{126,1.083,20.775},{115,0.645,0.98},
        {103,0.636,4694.003},{102,0.976,15720.839},{102,4.267,7.114},
        {99,6.21,2146.17},{98,0.68,155.42},{86,5.98,161000.69},
        {85,1.3,6275.96},{85,3.67,71430.7},{80,1.81,17260.15},
        {79,3.04,12036.46},{75,1.76,5088.63},{74,3.5,3154.69},
        {74,4.68,801.82},{70,0.83,9437.76},{62,3.98,8827.39},
        {61,1.82,7084.9},{57,2.78,6286.6},{56,4.39,14143.5},
        {56,3.47,6279.55},{52,0.19,12139.55},{52,1.33,1748.02},
        {51,0.28,5856.48},{49,0.49,1194.45},{41,5.37,8429.24},
        {41,2.4,19651.05},{39,6.17,10447.39},{37,6.04,10213.29},
        {37,2.57,1059.38},{36,1.71,2352.87},{36,1.78,6812.77},
        {33,0.59,17789.85},{30,0.44,83996.85},{30,2.74,1349.87},{25,3.16,4690.48}
    };
    static readonly double[,] EarthL1 = {
        {628331966747,0,0},{206059,2.678235,6283.07585},{4303,2.6351,12566.1517},
        {425,1.59,3.523},{119,5.796,26.298},{109,2.966,1577.344},
        {93,2.59,18849.23},{72,1.14,529.69},{68,1.87,398.15},
        {67,4.41,5507.55},{59,2.89,5223.69},{56,2.17,155.42},
        {45,0.4,796.3},{36,0.47,775.52},{29,2.65,7.11},
        {21,5.34,0.98},{19,1.85,5486.78},{19,4.97,213.3},
        {17,2.99,6275.96},{16,0.03,2544.31},{16,1.43,2146.17},
        {15,1.21,10977.08},{12,2.83,1748.02},{12,3.26,5088.63},
        {12,5.27,1194.45},{12,2.08,4694},{11,0.77,553.57},
        {10,1.3,6286.6},{10,4.24,1349.87},{9,2.7,242.73},
        {9,5.64,951.72},{8,5.3,2352.87},{6,2.65,9437.76},{6,4.67,4690.48}
    };
    static readonly double[,] EarthL2 = {
        {52919,0,0},{8720,1.0721,6283.0758},{309,0.867,12566.152},
        {27,0.05,3.52},{16,5.19,26.3},{16,3.68,155.42},
        {10,0.76,18849.23},{9,2.06,77713.77},{7,0.83,775.52},
        {5,4.66,1577.34},{4,1.03,7.11},{4,3.44,5573.14},
        {3,5.14,796.3},{3,6.05,5507.55},{3,1.19,242.73},
        {3,6.12,529.69},{3,0.31,398.15},{3,2.28,553.57},{2,4.38,5223.69},{2,3.75,0.98}
    };
    static readonly double[,] EarthL3 = {
        {289,5.844,6283.076},{35,0,0},{17,5.49,12566.15},
        {3,5.2,155.42},{1,4.72,3.52},{1,5.3,18849.23},{1,5.97,242.73}
    };
    static readonly double[,] EarthL4 = { {114,3.142,0},{8,4.13,6283.08},{1,3.84,12566.15} };
    static readonly double[,] EarthL5 = { {1,3.14,0} };

    static readonly double[,] EarthR0 = {
        {100013989,0,0},{1670700,3.0984635,6283.07585},{13956,3.05525,12566.1517},
        {3084,5.1985,77713.7715},{1628,1.1739,5753.3849},{1576,2.8469,7860.4194},
        {925,5.453,11506.77},{542,4.564,3930.21},{472,3.661,5884.927},
        {346,0.964,5507.553},{329,5.9,5223.694},{307,0.299,5573.143},
        {243,4.273,11790.629},{212,5.847,1577.344},{186,5.022,10977.079},
        {175,3.012,18849.228},{110,5.055,5486.778},{98,0.89,6069.78},
        {86,5.69,15720.84},{86,1.27,161000.69},{65,0.27,17260.15},
        {63,0.92,529.69},{57,2.01,83996.85},{56,5.24,71430.7},
        {49,3.25,2544.31},{47,2.58,775.52},{45,5.54,9437.76},
        {43,6.01,6275.96},{39,5.36,4694},{38,2.39,8827.39},
        {37,0.83,19651.05},{37,4.9,12139.55},{36,1.67,12036.46}
    };

    static double Series(double[,] terms, double tau)
    {
        double sum = 0;
        for (int i = 0; i < terms.GetLength(0); i++)
            sum += terms[i, 0] * Math.Cos(terms[i, 1] + terms[i, 2] * tau);
        return sum;
    }

    /// <summary>Apparent geocentric longitude of the Sun in degrees (of date). Meeus ch. 25, VSOP87.</summary>
    public static double SunApparentLongitude(double jde)
    {
        double t = T(jde);
        double tau = t / 10.0;

        double l = (Series(EarthL0, tau) + Series(EarthL1, tau) * tau + Series(EarthL2, tau) * tau * tau
                 + Series(EarthL3, tau) * Math.Pow(tau, 3) + Series(EarthL4, tau) * Math.Pow(tau, 4)
                 + Series(EarthL5, tau) * Math.Pow(tau, 5)) / 1e8;                    // radians
        double r = (Series(EarthR0, tau)) / 1e8;                                       // AU

        double earthLonDeg = Norm360(l / Deg);
        double theta = Norm360(earthLonDeg + 180.0);                                   // Sun geometric longitude

        // FK5 correction (Meeus 25.9)
        double lambdaPrime = theta - 1.397 * t - 0.00031 * t * t;
        theta += (-0.09033 + 0.03916 * (Math.Cos(lambdaPrime * Deg) + Math.Sin(lambdaPrime * Deg)) * Math.Tan(0.0)) / 3600.0;

        // nutation in longitude + annual aberration
        double apparent = theta + NutationLongitude(jde) / 3600.0 - (20.4898 / r) / 3600.0;
        return Norm360(apparent);
    }

    // ---- Moon: abridged ELP-2000/82, Meeus ch. 47 Table 47.A (Σl, units 1e-6 deg) ----
    static readonly int[,] MoonArgs = {
        {0,0,1,0},{2,0,-1,0},{2,0,0,0},{0,0,2,0},{0,1,0,0},{0,0,0,2},{2,0,-2,0},{2,-1,-1,0},
        {2,0,1,0},{2,-1,0,0},{0,1,-1,0},{1,0,0,0},{0,1,1,0},{2,0,0,-2},{0,0,1,2},{0,0,1,-2},
        {4,0,-1,0},{0,0,3,0},{4,0,-2,0},{2,1,-1,0},{2,1,0,0},{1,0,-1,0},{1,1,0,0},{2,-1,1,0},
        {2,0,2,0},{4,0,0,0},{2,0,-3,0},{0,1,-2,0},{2,0,-1,2},{2,-1,-2,0},{1,0,1,0},{2,-2,0,0},
        {0,1,2,0},{0,2,0,0},{2,-2,-1,0},{2,0,1,-2},{2,0,0,2},{4,-1,-1,0},{0,0,2,2},{3,0,-1,0},
        {2,1,1,0},{4,-1,-2,0},{0,2,-1,0},{2,2,-1,0},{2,1,-2,0},{2,-1,0,-2},{4,0,1,0},{0,0,4,0},
        {4,-1,0,0},{1,0,-2,0},{2,1,0,-2},{0,0,2,-2},{1,1,1,0},{3,0,-2,0},{4,0,-3,0},{2,-1,2,0},
        {0,2,1,0},{1,1,-1,0},{2,0,3,0},{2,0,-1,-2}
    };
    static readonly double[] MoonCoef = {
        6288774,1274027,658314,213618,-185116,-114332,58793,57066,
        53322,45758,-40923,-34720,-30383,15327,-12528,10980,
        10675,10034,8548,-7888,-6766,-5163,4987,4036,
        3994,3861,3665,-2689,-2602,2390,-2348,2236,
        -2120,-2069,2048,-1773,-1595,1215,-1110,-892,
        -810,759,-713,-700,691,596,549,537,
        520,-487,-399,-381,351,-340,330,327,
        -323,299,294,-197
    };

    /// <summary>Apparent geocentric longitude of the Moon in degrees (of date). Meeus ch. 47.</summary>
    public static double MoonApparentLongitude(double jde)
    {
        double t = T(jde);

        double lp = 218.3164477 + 481267.88123421 * t - 0.0015786 * t * t
                  + t * t * t / 538841.0 - t * t * t * t / 65194000.0;
        double d = 297.8501921 + 445267.1114034 * t - 0.0018819 * t * t
                 + t * t * t / 545868.0 - t * t * t * t / 113065000.0;
        double m = 357.5291092 + 35999.0502909 * t - 0.0001536 * t * t + t * t * t / 24490000.0;
        double mp = 134.9633964 + 477198.8675055 * t + 0.0087414 * t * t
                  + t * t * t / 69699.0 - t * t * t * t / 14712000.0;
        double f = 93.2720950 + 483202.0175233 * t - 0.0036539 * t * t
                 - t * t * t / 3526000.0 + t * t * t * t / 863310000.0;

        double e = 1.0 - 0.002516 * t - 0.0000074 * t * t;

        double sumL = 0;
        for (int i = 0; i < MoonCoef.Length; i++)
        {
            int ad = MoonArgs[i, 0], am = MoonArgs[i, 1], amp = MoonArgs[i, 2], af = MoonArgs[i, 3];
            double arg = ad * d + am * m + amp * mp + af * f;
            double coef = MoonCoef[i];
            int absM = Math.Abs(am);
            if (absM == 1) coef *= e;
            else if (absM == 2) coef *= e * e;
            sumL += coef * Sin(arg);
        }

        // additive terms (Meeus p. 342)
        double a1 = 119.75 + 131.849 * t;
        double a2 = 53.09 + 479264.290 * t;
        sumL += 3958 * Sin(a1) + 1962 * Sin(lp - f) + 318 * Sin(a2);

        double lambda = lp + sumL / 1_000_000.0;
        lambda += NutationLongitude(jde) / 3600.0;   // apparent
        return Norm360(lambda);
    }

    /// <summary>
    /// Lahiri (Chitrapaksha) ayanamsa in degrees. Base value at J2000 with IAU-2006
    /// general precession in longitude; calibrated to the standard Lahiri series.
    /// </summary>
    public static double Ayanamsa(double jd)
    {
        double t = T(jd);
        double precession = 5028.796195 * t + 1.1054348 * t * t;   // arcsec since J2000
        // Base calibrated to the standard Lahiri series (measured against a reference
        // implementation across 2026-2027; residual well under 1 arcsec).
        return 23.857054 + precession / 3600.0;
    }

    public static double SunSidereal(double jdUt)
    {
        double jde = jdUt + DeltaTSeconds(jdUt) / 86400.0;
        return Norm360(SunApparentLongitude(jde) - Ayanamsa(jdUt));
    }

    public static double MoonSidereal(double jdUt)
    {
        double jde = jdUt + DeltaTSeconds(jdUt) / 86400.0;
        return Norm360(MoonApparentLongitude(jde) - Ayanamsa(jdUt));
    }

    // ---------- Sunrise / sunset (Meeus ch. 15, iterative) ----------

    /// <summary>Sun's apparent right ascension and declination (degrees) at a given JDE.</summary>
    static (double Ra, double Dec) SunEquatorial(double jde)
    {
        double t = T(jde);
        double lambda = SunApparentLongitude(jde);
        double eps0 = 23.0 + 26.0 / 60.0 + 21.448 / 3600.0
                    - (46.8150 * t + 0.00059 * t * t - 0.001813 * t * t * t) / 3600.0;
        double omega = 125.04 - 1934.136 * t;
        double eps = eps0 + 0.00256 * Cos(omega);   // apparent obliquity

        double ra = Math.Atan2(Cos(eps) * Sin(lambda), Cos(lambda)) / Deg;
        double dec = Math.Asin(Sin(eps) * Sin(lambda)) / Deg;
        return (Norm360(ra), dec);
    }

    /// <summary>Apparent sidereal time at Greenwich, degrees. Meeus ch. 12.</summary>
    static double GreenwichSiderealTime(double jdUt)
    {
        double t = T(jdUt);
        double theta = 280.46061837 + 360.98564736629 * (jdUt - 2451545.0)
                     + 0.000387933 * t * t - t * t * t / 38710000.0;
        return Norm360(theta);
    }

    /// <summary>
    /// Local sunrise/sunset as Julian Days (UT). Longitude EAST positive.
    /// h0 = −0.8333° accounts for refraction plus the solar semi-diameter (upper limb).
    /// Returns null when the sun does not rise/set that day.
    /// </summary>
    public static (double? Rise, double? Set) SunRiseSet(double jdMidnightUt, double latitude, double longitude)
    {
        const double h0 = -0.8333;
        double? Solve(bool rising)
        {
            // start from local noon and iterate
            double guess = jdMidnightUt + 0.5 - longitude / 360.0 + (rising ? -0.25 : 0.25);
            for (int i = 0; i < 6; i++)
            {
                double jde = guess + DeltaTSeconds(guess) / 86400.0;
                var (ra, dec) = SunEquatorial(jde);

                double cosH = (Sin(h0) - Sin(latitude) * Sin(dec)) / (Cos(latitude) * Cos(dec));
                if (cosH < -1 || cosH > 1) return null;          // circumpolar
                double h = Math.Acos(cosH) / Deg;                // hour angle, degrees
                if (rising) h = -h;

                double theta = GreenwichSiderealTime(guess);
                double currentH = Norm360(theta + longitude - ra);
                if (currentH > 180) currentH -= 360;

                double delta = (h - currentH);
                while (delta > 180) delta -= 360;
                while (delta < -180) delta += 360;

                guess += delta / 360.98564736629;
                if (Math.Abs(delta) < 1e-6) break;
            }
            return guess;
        }
        return (Solve(true), Solve(false));
    }
}

using Sanathana.Companion.Application.Panchangam;

namespace Sanathana.Companion.Infrastructure.Localization;

/// <summary>
/// The controlled vocabulary the Panchangam generator can ever emit, read straight from the code
/// tables that produce it.
/// </summary>
/// <remarks>
/// Sourcing this from <see cref="PanchangamTables"/> rather than from a hand-written list means the
/// dictionary cannot drift from the calculator: adding a nakshatra there makes it appear here for
/// translation automatically. Roughly 150 terms cover all 1460+ stored rows, because every row is
/// built from these words plus clock times.
/// </remarks>
public static class PanchangamTermSeed
{
    public const string Category = "panchangam";

    public static IReadOnlyList<string> All()
    {
        var terms = new List<string>();

        // Weekday names as STORED — the calculator bakes the English forms.
        terms.AddRange(PanchangamTables.WeekdaysEnglish);

        terms.AddRange(PanchangamTables.Tithis);
        terms.Add("Amavasya");              // special-cased in the calculator, not in the Tithis array

        terms.AddRange(PanchangamTables.Nakshatras);

        foreach (var masam in PanchangamTables.Masams)
        {
            terms.Add(masam);
            terms.Add($"Adhika {masam}");   // leap-month prefix, must beat the bare month name
        }

        terms.AddRange(PanchangamTables.RutuvuByMasam.Distinct());
        terms.AddRange(PanchangamTables.Samvatsaras);

        // Literals composed inline by PanchangamCalculator.
        terms.Add("Shukla");
        terms.Add("Krishna (Bahula)");
        terms.Add("Uttarayanam");
        terms.Add("Dakshinayanam");
        terms.Add("None");

        // Connectives from FormatSpans: "{name} upto {t}", "{name} from {t}", "{name} full day".
        terms.Add("upto");
        terms.Add("from");
        terms.Add("full day");

        return terms
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
    }
}

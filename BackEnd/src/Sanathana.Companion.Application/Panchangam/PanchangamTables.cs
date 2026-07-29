namespace Sanathana.Companion.Application.Panchangam;

/// <summary>
/// Traditional Telugu (Drik / Amanta / sunrise-start) Panchangam constants.
/// Every table here was cross-checked against published Drik Panchang output for
/// Hyderabad on at least two dates per weekday before being committed.
/// </summary>
public static class PanchangamTables
{
    public static readonly string[] Weekdays =
        { "Aadivaram", "Somavaram", "Mangalavaram", "Budhavaram", "Guruvaram", "Sukravaram", "Sanivaram" };

    public static readonly string[] WeekdaysEnglish =
        { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    /// <summary>15 tithi names; index 15 is Pournami in Shukla and Amavasya in Krishna paksha.</summary>
    public static readonly string[] Tithis =
    {
        "Padyami", "Vidiya", "Tadiya", "Chaviti", "Panchami", "Shashti", "Saptami", "Ashtami",
        "Navami", "Dasami", "Ekadasi", "Dvadasi", "Trayodasi", "Chaturdasi", "Pournami"
    };

    /// <summary>27 nakshatras in Telugu forms (deliberately not the Sanskrit forms).</summary>
    public static readonly string[] Nakshatras =
    {
        "Aswini", "Bharani", "Krittika", "Rohini", "Mrigasira", "Arudra", "Punarvasu", "Pushyami",
        "Aslesha", "Makha", "Pubba", "Uttara", "Hasta", "Chitta", "Swati", "Visakha", "Anuradha",
        "Jyeshta", "Moola", "Purvashada", "Uttarashada", "Sravanam", "Dhanishta", "Satabhisham",
        "Purvabhadra", "Uttarabhadra", "Revati"
    };

    /// <summary>12 Amanta months, named by the rashi the Sun enters during the lunation.</summary>
    public static readonly string[] Masams =
    {
        "Chaitram", "Vaisakham", "Jyeshtham", "Ashadham", "Sravanam", "Bhadrapadam",
        "Asvayujam", "Kartikam", "Margasiram", "Pushyam", "Magham", "Phalgunam"
    };

    /// <summary>Rutuvu (season) for each Amanta month index 0..11.</summary>
    public static readonly string[] RutuvuByMasam =
    {
        "Vasantha", "Vasantha", "Greeshma", "Greeshma", "Varsha", "Varsha",
        "Sarad", "Sarad", "Hemantha", "Hemantha", "Sisira", "Sisira"
    };

    /// <summary>The 60-year Jovian cycle. Name = Samvatsaras[(shakaYear + 11) % 60].</summary>
    public static readonly string[] Samvatsaras =
    {
        "Prabhava", "Vibhava", "Sukla", "Pramoduta", "Prajotpatti", "Angirasa", "Srimukha", "Bhava",
        "Yuva", "Dhata", "Iswara", "Bahudhanya", "Pramadi", "Vikrama", "Vishu", "Chitrabhanu",
        "Swabhanu", "Tarana", "Parthiva", "Vyaya", "Sarvajit", "Sarvadhari", "Virodhi", "Vikruti",
        "Khara", "Nandana", "Vijaya", "Jaya", "Manmatha", "Durmukhi", "Hevilambi", "Vilambi",
        "Vikari", "Sarvari", "Plava", "Subhakrutu", "Sobhakrutu", "Krodhi", "Vishvavasu", "Parabhava",
        "Plavanga", "Kilaka", "Saumya", "Sadharana", "Virodhikrutu", "Paridhavi", "Pramadicha",
        "Ananda", "Rakshasa", "Nala", "Pingala", "Kalayukti", "Siddharthi", "Raudri", "Durmati",
        "Dundubhi", "Rudhirodgari", "Raktakshi", "Krodhana", "Akshaya"
    };

    // ---- Eighths of the sunrise→sunset arc, 1-based, indexed by DayOfWeek (Sun..Sat) ----
    // Signature checks: Thursday Yamagandam and Saturday Gulika both begin exactly at sunrise.
    public static readonly int[] RahuKalamPart = { 8, 2, 7, 5, 6, 4, 3 };
    public static readonly int[] YamagandamPart = { 5, 4, 3, 2, 1, 7, 6 };
    public static readonly int[] GulikaPart = { 7, 6, 5, 4, 3, 2, 1 };

    /// <summary>A Durmuhurtham slot: 1-based muhurta index out of 15, by day or by night.</summary>
    public readonly record struct MuhurtaSlot(int Index, bool IsNight);

    /// <summary>
    /// Durmuhurtham slots per weekday (Sun..Sat). Tuesday's second slot is a NIGHT
    /// muhurta — counted from sunset, not sunrise.
    /// </summary>
    public static readonly MuhurtaSlot[][] Durmuhurtham =
    {
        new[] { new MuhurtaSlot(14, false) },                                 // Sunday
        new[] { new MuhurtaSlot(9, false), new MuhurtaSlot(12, false) },      // Monday
        new[] { new MuhurtaSlot(4, false), new MuhurtaSlot(7, true) },        // Tuesday (night)
        new[] { new MuhurtaSlot(8, false) },                                  // Wednesday (= Abhijit slot)
        new[] { new MuhurtaSlot(6, false), new MuhurtaSlot(12, false) },      // Thursday
        new[] { new MuhurtaSlot(4, false), new MuhurtaSlot(9, false) },       // Friday
        new[] { new MuhurtaSlot(1, false), new MuhurtaSlot(2, false) }        // Saturday
    };

    /// <summary>Abhijit is day-muhurta 8. It is not observed on Wednesday, where that slot is Durmuhurtham.</summary>
    public const int AbhijitMuhurtaIndex = 8;

    /// <summary>
    /// Varjyam start as ELAPSED ghatikas out of the nakshatra's 60. Moola is the only
    /// nakshatra with two windows. Verified 27/27 against the published table.
    /// </summary>
    public static readonly int[][] VarjyamGhatikas =
    {
        new[] { 50 }, new[] { 24 }, new[] { 30 }, new[] { 40 }, new[] { 14 }, new[] { 21 },
        new[] { 30 }, new[] { 20 }, new[] { 32 }, new[] { 30 }, new[] { 20 }, new[] { 18 },
        new[] { 21 }, new[] { 20 }, new[] { 14 }, new[] { 14 }, new[] { 10 }, new[] { 14 },
        new[] { 20, 56 }, new[] { 24 }, new[] { 20 }, new[] { 10 }, new[] { 10 }, new[] { 18 },
        new[] { 16 }, new[] { 24 }, new[] { 30 }
    };

    /// <summary>
    /// Amrutha Kalam start as elapsed ghatikas out of 60. Usually Varjyam + 24, but not
    /// always — Aswini is −8 and Rohini/Arudra/Hasta differ, so this is tabulated, not derived.
    /// </summary>
    public static readonly int[] AmruthaGhatikas =
    {
        42, 48, 54, 52, 38, 35, 54, 44, 56, 54, 44, 42, 45, 44, 38, 38, 34, 38,
        44, 48, 44, 34, 34, 42, 40, 48, 54
    };

    /// <summary>Varjyam and Amrutha Kalam both run for 4 ghatikas (1/15 of the nakshatra span).</summary>
    public const int WindowGhatikas = 4;
    public const int GhatikasPerNakshatra = 60;
}

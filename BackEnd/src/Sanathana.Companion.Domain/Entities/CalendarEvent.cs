using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A mark a seeker puts on their own calendar: a vratam they keep, a puja they have planned, a
/// family anniversary observed by date. Shown on the calendar beside the festivals and the
/// Panchangam, and nowhere else.
/// </summary>
/// <remarks>
/// Like <see cref="UserFavorite"/> it holds a plain user id rather than a foreign key; the account
/// purge in UserRepository removes a seeker's rows with the rest of their data, and the data export
/// in AccountDataReader lists them.
/// </remarks>
public class CalendarEvent : BaseEntity
{
    public const int MaxTitleLength = 120;
    public const int MaxNotesLength = 1000;
    public const int MaxKindLength = 30;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>The civil date. For a yearly repeat this is the FIRST occurrence.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Optional clock time (IST), for a mark that is at an hour rather than a day.</summary>
    public TimeOnly? Time { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>One of <see cref="CalendarEventKinds"/>; drives the colour on the grid.</summary>
    public string Kind { get; set; } = CalendarEventKinds.Personal;

    /// <summary>Repeat on the same month and day every year (a birthday, a death anniversary by date).</summary>
    public bool RepeatYearly { get; set; }
}

/// <summary>The kinds a seeker can file a calendar mark under. Stored as-is, so the casing here is the casing in the database.</summary>
public static class CalendarEventKinds
{
    public const string Puja = "Puja";
    public const string Vratam = "Vratam";
    public const string Festival = "Festival";
    public const string Birthday = "Birthday";
    public const string Anniversary = "Anniversary";
    public const string Shraddha = "Shraddha";
    public const string Personal = "Personal";

    public static readonly IReadOnlyList<string> All = new[] { Puja, Vratam, Festival, Birthday, Anniversary, Shraddha, Personal };

    /// <summary>The canonical spelling of a kind the client sent, or null when it is not one of ours.</summary>
    public static string? Normalize(string? kind)
        => string.IsNullOrWhiteSpace(kind) ? null
         : All.FirstOrDefault(k => k.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase));
}

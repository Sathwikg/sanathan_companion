using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One notification type an administrator has made available: which form it belongs to, what kind
/// of notification it is, and the few numbers that decide when it goes out. A form with no row here
/// cannot raise notifications.
/// </summary>
/// <remarks>
/// A form can hold several types (Panchangam has the morning summary and a warning for each
/// inauspicious window), told apart by <see cref="Kind"/>. The wording of the push itself is not
/// stored here: it lives in the language dictionary under the "push" namespace, so it is edited per
/// language on the Language Configs screen like every other piece of text.
/// </remarks>
public class NotificationConfig : BaseEntity
{
    /// <summary>What <see cref="Kind"/> holds for a plain daily reminder the phone schedules by itself.</summary>
    public const string ReminderKind = "reminder";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The notification type's code (see NotificationKinds in the Application layer). "reminder" is
    /// the plain daily reminder any form can have; every other value is delivered by the server.
    /// </summary>
    public string Kind { get; set; } = ReminderKind;

    /// <summary>The form/module this notification belongs to.</summary>
    public Guid MenuModuleId { get; set; }
    public MenuModule? MenuModule { get; set; }

    /// <summary>Headline shown to users; falls back to the module name when blank.</summary>
    public string? Title { get; set; }

    /// <summary>Explains to the user what this notification is for.</summary>
    public string? Description { get; set; }

    /// <summary>Whether users receive it unless they opt out (users with no saved preference follow this).</summary>
    public bool DefaultEnabledForUsers { get; set; } = true;

    /// <summary>Users cannot turn this one off (e.g. important announcements).</summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// The clock time (the seeker's own) this type goes out at, for the types sent once a day. For a
    /// type the seeker times themselves it is the time used until they choose one. Null follows the
    /// type's built-in default.
    /// </summary>
    public TimeOnly? SendTime { get; set; }

    /// <summary>How many minutes before a Panchangam window opens its warning is sent. Null follows the default.</summary>
    public int? LeadMinutes { get; set; }

    /// <summary>The shortest streak (in days) the streak notifications speak about. Null follows the default.</summary>
    public int? Threshold { get; set; }

    /// <summary>Optional picture shown with the notification (an https URL). Null sends text only.</summary>
    public string? ImageUrl { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Admin master switch for this notification type.</summary>
    public bool IsActive { get; set; } = true;
}

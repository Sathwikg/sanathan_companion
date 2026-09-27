namespace Sanathana.Companion.Application.Common;

/// <summary>Settings for "Sign in with Google", bound from the <c>Google</c> configuration section.</summary>
/// <remarks>
/// A Google ID token names the OAuth client it was minted for in its <c>aud</c> claim, and the API
/// accepts only tokens minted for OUR clients. Otherwise any site the seeker ever signed into with
/// Google could present that site's token here. The web button and the Android Credential Manager
/// flow both use the "Web application" client id, so one entry is enough today; the iOS client id
/// joins the list when that head gets the feature.
/// </remarks>
public class GoogleSignInOptions
{
    public const string SectionName = "Google";

    public string[] ClientIds { get; set; } = [];

    /// <summary>
    /// How long the ticket handed back for the register / link steps stays valid. Ten minutes is
    /// long enough to type a mobile number and a password, and short enough that a ticket lifted
    /// from a log or a screenshot is dead by the time anyone reads it.
    /// </summary>
    public int TicketMinutes { get; set; } = 10;

    public bool IsConfigured => ClientIds.Any(c => !string.IsNullOrWhiteSpace(c));
}

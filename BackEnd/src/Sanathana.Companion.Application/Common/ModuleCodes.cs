namespace Sanathana.Companion.Application.Common;

/// <summary>
/// The stable name of every form, used to tie an API endpoint to the module an administrator
/// grants or withholds on the Access Rights screen.
/// </summary>
/// <remarks>
/// Not the module's Name, because two seeded rows are both called "Puja Process" — a name is not a
/// key. Not its RoutePath either, because that is free text an administrator can edit, so fixing a
/// typo in a URL would silently change who may call an endpoint. The code is derived once, in the
/// migration, and then frozen; it is absent from the create and update DTOs, so the Modules form
/// cannot rename or forge one.
/// <para>
/// The values are exactly what <c>LocalizationService.NamespaceForRoute</c> produces for each
/// route, which is why they are also the folder names under Localization/Resources. Dashboard is
/// the one exception: its route is "/" and that rule yields an empty string, so it is named here.
/// </para>
/// </remarks>
public static class ModuleCodes
{
    public const string AdminDashboard = "adminDashboard";
    public const string Dashboard = "dashboard";
    public const string Modules = "modules";
    public const string Regions = "regions";
    public const string Festivals = "festivals";
    public const string Deities = "deities";
    public const string Chants = "chants";
    public const string Users = "users";
    public const string Languages = "languages";
    public const string Roles = "roles";
    public const string ChantsConfig = "chantsConfig";
    public const string Panchangam = "panchangam";
    public const string PujaProcessConfig = "pujaProcessConfig";
    public const string PujaProcess = "pujaProcess";
    public const string Pujas = "pujas";
    public const string Wallpapers = "wallpapers";
    public const string WallpapersDownload = "wallpapersDownload";
    public const string Sadhana = "sadhana";
    public const string AccessRights = "accessRights";
    public const string Feedback = "feedback";
    public const string FeedbackDashboard = "feedbackDashboard";
    public const string IssueTypes = "issueTypes";
    public const string Favorites = "favorites";
    public const string NotificationConfig = "notificationConfig";
    public const string MyNotifications = "myNotifications";
    public const string LanguageConfigs = "languageConfigs";
}

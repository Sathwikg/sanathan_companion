namespace App.Core.Config;

/// <summary>
/// Every REST path the clients call, in one place.
/// </summary>
/// <remarks>
/// Paths are relative to <see cref="AppConfig.ApiBaseUrl"/> and therefore carry no leading slash —
/// <c>HttpClient</c> resolves a leading-slash path against the authority and would drop the
/// <c>/api</c> segment the base address ends with.
/// <para>
/// The point of this file is that adding, renaming or versioning an endpoint is a one-file edit
/// that the compiler then walks through every call site of. Query strings that vary per call are
/// built by the small helpers at the bottom rather than concatenated at the call site, so escaping
/// is applied in exactly one place too.
/// </para>
/// </remarks>
public static class ApiRoutes
{
    public static class Auth
    {
        public const string Register = "auth/register";
        public const string Login = "auth/login";
        public const string ChangePassword = "auth/change-password";
    }

    public static class Dashboard
    {
        public const string Mine = "dashboard";
        public const string Admin = "dashboard/admin";
        public static string TodayBhakti(Guid? regionId) => Query("dashboard/today-bhakti", ("regionId", regionId));
        public static string Prayers(Guid? regionId) => Query("dashboard/prayers", ("regionId", regionId));
    }

    public static class MenuModules
    {
        public const string Root = "menumodules";
        public const string Tree = "menumodules/tree";
        /// <summary>The navigation menu, filtered server-side by the caller's role and this platform.</summary>
        public static string Menu(string platform) => Query("menumodules/menu", ("platform", platform));
        public static string ById(Guid id) => $"menumodules/{id}";
        public static string Status(Guid id) => $"menumodules/{id}/status";
    }

    public static class Roles
    {
        public const string Root = "roles";
        public static string Search(string? search) => Query("roles", ("search", search));
        public static string ById(int roleId) => $"roles/{roleId}";
    }

    public static class AccessRights
    {
        public const string Roles = "accessrights/roles";
        public static string ForRole(int roleId) => $"accessrights/{roleId}";
    }

    public static class Localization
    {
        public const string Locales = "localization/locales";
        public const string Matrix = "localization/matrix";
        public const string EntityMatrix = "localization/entity-matrix";
        public const string Dictionary = "localization/dictionary";
        public const string HarvestDictionary = "localization/dictionary/harvest";

        public static string Bundle(string code) => $"localization/bundle/{Escape(code)}";
        public static string Labels(Guid languageId) => $"localization/labels/{languageId}";
        public static string Forms(Guid languageId) => $"localization/forms/{languageId}";
        public static string Entities(Guid languageId) => $"localization/entities/{languageId}";
        public static string Export(Guid languageId) => $"localization/export/{languageId}";
        public static string MatrixScoped(string? scope) => Query("localization/matrix", ("scope", scope));

        public static string DictionaryPage(string? category, string? search, bool missingOnly, int page, int pageSize)
            => Query("localization/dictionary",
                ("page", page),
                ("pageSize", pageSize),
                ("missingOnly", missingOnly ? "true" : null),
                ("category", category),
                ("search", search));
    }

    public static class Regions
    {
        public const string Root = "regions";
        public const string Options = "regions/options";
        public static string ById(Guid id) => $"regions/{id}";
        public static string Status(Guid id) => $"regions/{id}/status";
    }

    public static class Festivals
    {
        public const string Root = "festivals";
        public const string Years = "festivals/years";
        public static string ForYear(int year) => Query("festivals", ("year", year));
        public static string ById(Guid id) => $"festivals/{id}";
        public static string Status(Guid id) => $"festivals/{id}/status";
    }

    public static class Deities
    {
        public const string Root = "deities";
        public const string FormOptions = "deities/form-options";
        public static string ById(Guid id) => $"deities/{id}";
        public static string Status(Guid id) => $"deities/{id}/status";
    }

    public static class Users
    {
        public const string Root = "users";
        public static string ById(Guid id) => $"users/{id}";
    }

    public static class Profile
    {
        public const string Me = "profile/me";
        public const string Region = "profile/region";
        /// <summary>Everything the app holds about the caller, as a JSON download.</summary>
        public const string Export = "profile/export";
        /// <summary>DELETE — erases the caller's account and everything belonging to them.</summary>
        public const string DeleteMe = "profile/me";
    }

    public static class Sadhana
    {
        public const string Log = "sadhana/log";
        public const string Streak = "sadhana/streak";
        public static string Today(Guid? regionId) => Query("sadhana/today", ("regionId", regionId));
        public static string Chants(string? search, Guid? regionId) => Query("sadhana/chants", ("search", search), ("regionId", regionId));
        public static string Chant(Guid id) => $"sadhana/chants/{id}";
    }

    public static class Panchangam
    {
        public const string Root = "panchangam";
        public const string Options = "panchangam/options";
        public const string Generate = "panchangam/generate";

        public static string List(int? year, Guid? regionId, DateOnly? from, DateOnly? to, string? search)
            => Query("panchangam",
                ("year", year),
                ("regionId", regionId),
                ("from", from),
                ("to", to),
                ("search", search));

        public static string ByDate(DateOnly date, Guid regionId)
            => Query("panchangam/by-date", ("date", date), ("regionId", regionId));

        public static string Compute(double lat, double lon, DateOnly? date, string? place)
            => Query("panchangam/compute", ("lat", lat), ("lon", lon), ("date", date), ("place", place));
    }

    public static class Languages
    {
        public const string Root = "languages";
        public const string ByRegion = "languages/by-region";
        public static string List(Guid? regionId, string? search) => Query("languages", ("regionId", regionId), ("search", search));
        public static string ById(Guid id) => $"languages/{id}";
        public static string Status(Guid id) => $"languages/{id}/status";
    }

    public static class Chants
    {
        public const string Root = "chants";
        public static string ById(Guid id) => $"chants/{id}";
        public static string Status(Guid id) => $"chants/{id}/status";
    }

    public static class ChantConfigs
    {
        public const string Root = "chantconfigs";
        public const string FormOptions = "chantconfigs/form-options";
        public static string List(Guid? chantId, Guid? deityId, string? search) => Query("chantconfigs", ("chantId", chantId), ("deityId", deityId), ("search", search));
        public static string ById(Guid id) => $"chantconfigs/{id}";
        public static string Status(Guid id) => $"chantconfigs/{id}/status";
    }

    public static class Notifications
    {
        /// <summary>Admin: which modules may notify at all.</summary>
        public const string Config = "notificationconfig";
        /// <summary>The signed-in user's own preferences, and whether each would fire right now.</summary>
        public const string Mine = "notifications/me";
    }

    public static class IssueTypes
    {
        public const string Root = "issuetypes";
        public const string Active = "issuetypes/active";
        public static string ById(Guid id) => $"issuetypes/{id}";
        public static string Status(Guid id) => $"issuetypes/{id}/status";
    }

    public static class Favorites
    {
        public const string Root = "favorites";
        public const string Ids = "favorites/ids";
        public const string Toggle = "favorites/toggle";
    }

    public static class Feedback
    {
        public const string Root = "feedback";
        public const string Dashboard = "feedback/dashboard";
        public static string Status(Guid id) => $"feedback/{id}/status";
    }

    public static class Pujas
    {
        public const string Root = "pujas";
        public const string FormOptions = "pujas/form-options";
        public static string ById(Guid id) => $"pujas/{id}";
        public static string Status(Guid id) => $"pujas/{id}/status";
    }

    public static class PujaProcess
    {
        public const string Festivals = "pujaprocess/festivals";
        public static string Config(Guid pujaId) => $"pujaprocess/config/{pujaId}";
        public static string Pujas(Guid? festivalId) => Query("pujaprocess/pujas", ("festivalId", festivalId));
        public static string Puja(Guid pujaId) => $"pujaprocess/puja/{pujaId}";
    }

    public static class Wallpapers
    {
        public const string Root = "wallpapers";
        public static string Deities(bool onlyWithWallpapers) => Query("wallpapers/deities", ("onlyWithWallpapers", onlyWithWallpapers ? "true" : "false"));
        public static string ForDeity(Guid deityId, bool activeOnly) => Query($"wallpapers/deity/{deityId}", ("activeOnly", activeOnly ? "true" : "false"));
        public static string ById(Guid id) => $"wallpapers/{id}";
    }

    /// <summary>
    /// Endpoints that stream bytes rather than JSON, so a browser element fetches them directly
    /// instead of <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// These belong in an <c>src</c> or <c>href</c>, which means they need an ABSOLUTE url — the
    /// page's own origin is the app, not the API. Combine with
    /// <see cref="AppConfig.Absolute"/>: <c>Config.Absolute(ApiRoutes.Media.DeityImage(id))</c>.
    /// All four are <c>[AllowAnonymous]</c> server-side precisely so an &lt;img&gt; can reach them
    /// without a bearer token.
    /// </remarks>
    public static class Media
    {
        public static string DeityImage(Guid deityId) => $"deities/{deityId}/image";
        public static string ChantAudio(Guid chantConfigId) => $"chantconfigs/{chantConfigId}/audio";
        public static string WallpaperImage(Guid wallpaperId) => $"wallpapers/{wallpaperId}/image";
        /// <summary>Same bytes as <see cref="WallpaperImage"/>, but with a Content-Disposition that saves them.</summary>
        public static string WallpaperDownload(Guid wallpaperId) => $"wallpapers/{wallpaperId}/download";
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Appends only the parameters that carry a value, so an all-null call returns the bare path
    /// and never a dangling "?". Values are formatted the way the API's model binder expects:
    /// invariant for doubles (a comma decimal separator would be read as two arguments) and
    /// yyyy-MM-dd for dates.
    /// </summary>
    private static string Query(string path, params (string Key, object? Value)[] parameters)
    {
        var parts = new List<string>(parameters.Length);

        foreach (var (key, value) in parameters)
        {
            var formatted = Format(value);
            if (formatted is null) continue;
            parts.Add($"{key}={Escape(formatted)}");
        }

        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        DateOnly d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static string Escape(string value) => Uri.EscapeDataString(value);
}

using App.Core.Config;

namespace App.Tests;

/// <summary>
/// The route table is the contract with the API, and a typo in it fails at runtime as a 404 that
/// looks like a server problem. These assertions pin the exact strings the controllers expose.
/// </summary>
public class ApiRoutesTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Paths_are_relative_so_the_api_base_path_survives()
    {
        // A leading slash would resolve against the authority and drop the "/api" segment
        // the base address ends with, so every request would 404.
        foreach (var route in AllStaticRoutes())
            Assert.False(route.StartsWith('/'), $"'{route}' must not start with a slash.");
    }

    [Theory]
    [InlineData("auth/register")]
    [InlineData("auth/login")]
    [InlineData("dashboard")]
    [InlineData("dashboard/admin")]
    [InlineData("menumodules")]
    [InlineData("menumodules/tree")]
    [InlineData("roles")]
    [InlineData("accessrights/roles")]
    [InlineData("localization/locales")]
    [InlineData("regions")]
    [InlineData("regions/options")]
    [InlineData("festivals")]
    [InlineData("festivals/years")]
    [InlineData("deities")]
    [InlineData("deities/form-options")]
    [InlineData("users")]
    [InlineData("profile/me")]
    [InlineData("profile/region")]
    [InlineData("sadhana/log")]
    [InlineData("sadhana/streak")]
    [InlineData("panchangam")]
    [InlineData("panchangam/options")]
    [InlineData("panchangam/generate")]
    [InlineData("languages")]
    [InlineData("languages/by-region")]
    [InlineData("chants")]
    [InlineData("chantconfigs")]
    [InlineData("chantconfigs/form-options")]
    [InlineData("notificationconfig")]
    [InlineData("notifications/me")]
    [InlineData("issuetypes")]
    [InlineData("issuetypes/active")]
    [InlineData("favorites")]
    [InlineData("favorites/ids")]
    [InlineData("favorites/toggle")]
    [InlineData("feedback")]
    [InlineData("feedback/dashboard")]
    [InlineData("pujas")]
    [InlineData("pujas/form-options")]
    [InlineData("pujaprocess/festivals")]
    [InlineData("wallpapers")]
    public void Constant_routes_are_declared(string expected)
        => Assert.Contains(expected, AllStaticRoutes());

    [Fact]
    public void Menu_route_carries_the_platform()
    {
        Assert.Equal("menumodules/menu?platform=Mobile", ApiRoutes.MenuModules.Menu(PlatformNames.Mobile));
        Assert.Equal("menumodules/menu?platform=Web", ApiRoutes.MenuModules.Menu(PlatformNames.Web));
    }

    [Fact]
    public void Id_routes_interpolate_the_identifier()
    {
        Assert.Equal($"menumodules/{Id}", ApiRoutes.MenuModules.ById(Id));
        Assert.Equal($"menumodules/{Id}/status", ApiRoutes.MenuModules.Status(Id));
        Assert.Equal($"pujaprocess/config/{Id}", ApiRoutes.PujaProcess.Config(Id));
        Assert.Equal($"pujaprocess/puja/{Id}", ApiRoutes.PujaProcess.Puja(Id));
        Assert.Equal("roles/7", ApiRoutes.Roles.ById(7));
        Assert.Equal("accessrights/7", ApiRoutes.AccessRights.ForRole(7));
    }

    [Fact]
    public void Absent_query_parameters_are_dropped_entirely()
    {
        // Not "sadhana/today?" and not "sadhana/today?regionId=" — either would reach the
        // model binder as an empty value rather than as no value at all.
        Assert.Equal("sadhana/today", ApiRoutes.Sadhana.Today(null));
        Assert.Equal("sadhana/chants", ApiRoutes.Sadhana.Chants(null, null));
        Assert.Equal("roles", ApiRoutes.Roles.Search("   "));
        Assert.Equal("panchangam", ApiRoutes.Panchangam.List(null, null, null, null, null));
        Assert.Equal("pujaprocess/pujas", ApiRoutes.PujaProcess.Pujas(null));
    }

    [Fact]
    public void Present_query_parameters_are_appended_in_order()
    {
        Assert.Equal($"sadhana/today?regionId={Id}", ApiRoutes.Sadhana.Today(Id));
        Assert.Equal($"sadhana/chants?search=ram&regionId={Id}", ApiRoutes.Sadhana.Chants("ram", Id));
        Assert.Equal("festivals?year=2026", ApiRoutes.Festivals.ForYear(2026));
    }

    [Fact]
    public void Search_terms_are_trimmed_and_escaped()
    {
        Assert.Equal("roles?search=shri%20ram", ApiRoutes.Roles.Search("  shri ram  "));
        Assert.Equal("languages?search=a%26b", ApiRoutes.Languages.List(null, "a&b"));
    }

    [Fact]
    public void Dates_use_the_iso_form_the_model_binder_expects()
    {
        var date = new DateOnly(2026, 3, 9);
        Assert.Equal($"panchangam/by-date?date=2026-03-09&regionId={Id}", ApiRoutes.Panchangam.ByDate(date, Id));
        Assert.Equal("panchangam?from=2026-03-09", ApiRoutes.Panchangam.List(null, null, date, null, null));
    }

    [Fact]
    public void Compute_carries_no_coordinates_in_its_path()
    {
        // They travel in the body now, so that a seeker's position does not land in the access log
        // of every proxy between the phone and the API.
        Assert.Equal("panchangam/compute", ApiRoutes.Panchangam.Compute);
        Assert.DoesNotContain("lat", ApiRoutes.Panchangam.Compute);
    }

    [Fact]
    public void Booleans_are_lower_case_json_style()
    {
        Assert.Equal("wallpapers/deities?onlyWithWallpapers=true", ApiRoutes.Wallpapers.Deities(true));
        Assert.Equal("wallpapers/deities?onlyWithWallpapers=false", ApiRoutes.Wallpapers.Deities(false));
        Assert.Equal($"wallpapers/deity/{Id}?activeOnly=true", ApiRoutes.Wallpapers.ForDeity(Id, true));
    }

    [Fact]
    public void Dictionary_page_omits_the_missing_only_flag_when_it_is_off()
    {
        Assert.Equal("localization/dictionary?page=2&pageSize=50", ApiRoutes.Localization.DictionaryPage(null, null, false, 2, 50));
        Assert.Equal("localization/dictionary?page=1&pageSize=25&missingOnly=true&category=nav&search=om",
            ApiRoutes.Localization.DictionaryPage("nav", "om", true, 1, 25));
    }

    /// <summary>Every string constant declared anywhere in <see cref="ApiRoutes"/>, including nested groups.</summary>
    private static List<string> AllStaticRoutes()
    {
        var routes = new List<string>();
        Collect(typeof(ApiRoutes), routes);
        return routes;

        static void Collect(Type type, List<string> into)
        {
            foreach (var field in type.GetFields())
                if (field.IsLiteral && field.FieldType == typeof(string) && field.GetRawConstantValue() is string value)
                    into.Add(value);

            foreach (var nested in type.GetNestedTypes())
                Collect(nested, into);
        }
    }
}

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Every endpoint must say which form it belongs to, or say explicitly that it belongs to none.
/// </summary>
/// <remarks>
/// The module filter denies an authenticated non-Admin whose endpoint names no module, so a
/// forgotten attribute fails closed rather than open. That is the safe direction, but it fails at
/// run time on a real seeker; this test moves the failure to the build.
/// </remarks>
public class EndpointModuleMapTests
{
    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions()
    {
        var controllers = typeof(Sanathana.Companion.Api.Filters.ModuleAccessFilter).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controller in controllers)
        {
            var actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialAttribute() && m.GetCustomAttributes<HttpMethodAttribute>().Any());

            foreach (var action in actions) yield return (controller, action);
        }
    }

    private static bool IsMapped(Type controller, MethodInfo action)
        => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
        || action.GetCustomAttribute<ModuleExemptAttribute>() is not null
        || action.GetCustomAttribute<RequiresModuleAttribute>() is not null
        || controller.GetCustomAttribute<ModuleExemptAttribute>() is not null
        || controller.GetCustomAttribute<RequiresModuleAttribute>() is not null;

    [Fact]
    public void Every_action_names_a_module_or_says_it_has_none()
    {
        var unmapped = Actions()
            .Where(a => !IsMapped(a.Controller, a.Action))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .OrderBy(n => n)
            .ToList();

        Assert.True(unmapped.Count == 0,
            "These endpoints carry neither [RequiresModule], [ModuleExempt] nor [AllowAnonymous], so a " +
            "non-administrator would be refused at run time:" + Environment.NewLine +
            string.Join(Environment.NewLine, unmapped));
    }

    [Fact]
    public void Every_module_code_used_by_an_endpoint_is_a_real_one()
    {
        // A typo would deny the form silently: no role holds "chantsconfig", so the check simply
        // never matches.
        var known = typeof(ModuleCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet(StringComparer.Ordinal);

        var used = Actions()
            .SelectMany(a => new[]
            {
                a.Action.GetCustomAttribute<RequiresModuleAttribute>(),
                a.Controller.GetCustomAttribute<RequiresModuleAttribute>()
            })
            .Where(attr => attr is not null)
            .SelectMany(attr => attr!.Codes)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.All(used, code => Assert.Contains(code, known));
    }

    [Fact]
    public void The_exempt_set_is_small_and_deliberate()
    {
        // Not an arbitrary cap: exemption is the one way past the gate, so its membership should
        // be reviewed rather than grown by habit. Each of these is either session infrastructure
        // every role needs, or the caller's own data.
        var exempt = Actions()
            .Where(a => a.Action.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Where(a => a.Action.GetCustomAttribute<ModuleExemptAttribute>() is not null
                     || (a.Controller.GetCustomAttribute<ModuleExemptAttribute>() is not null
                         && a.Action.GetCustomAttribute<RequiresModuleAttribute>() is null))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[]
        {
            // Answers "should this screen show an ad". Whether the seeker may open the screen at
            // all is decided by that screen's own endpoints, so a role denied a form never asks.
            "AdsController.GetSlot",
            // A page visit by any signed-in role; recording it is not a privilege of any one form.
            "AuditTelemetryController.LogActivity",
            "AuthController.ChangePassword",
            "FavoritesController.GetIds",
            "FavoritesController.Toggle",
            "MediaController.GetTicket",
            "MenuModulesController.GetMenu",
            "NotificationsController.GetMine",
            "ProfileController.DeleteMyAccount",
            "ProfileController.ExportMyData",
            "ProfileController.Me",
            "ProfileController.SetDefaultRegion"
        }, exempt);
    }

    [Fact]
    public void The_guest_set_is_small_and_deliberate()
    {
        // [GuestAccess] is how a visitor with no account gets past [Authorize]. Each of these reads
        // shared content; none reads or writes anything that belongs to a person. Starting a chant
        // (SadhanaController.Chant), counting it (Log) and the streak are deliberately absent:
        // reaching for them is what asks a guest to sign in.
        var guest = Actions()
            .Where(a => a.Action.GetCustomAttribute<Sanathana.Companion.Api.Filters.GuestAccessAttribute>() is not null
                     || a.Controller.GetCustomAttribute<Sanathana.Companion.Api.Filters.GuestAccessAttribute>() is not null)
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[]
        {
            "DashboardController.Prayers",
            "DashboardController.TodayBhakti",
            "MediaController.GetTicket",
            "MenuModulesController.GetMenu",
            "PanchangamController.Compute",
            "PanchangamController.GetAll",
            "PanchangamController.GetByDate",
            "PanchangamController.GetById",
            "PanchangamController.GetOptions",
            "PujaProcessController.GetFestivals",
            "PujaProcessController.GetProcess",
            "PujaProcessController.GetPujas",
            "SadhanaController.Chants",
            "SadhanaController.Today",
            "WallpapersController.GetByDeity",
            "WallpapersController.GetById",
            "WallpapersController.GetDeities"
        }, guest);
    }

    [Fact]
    public void A_guest_endpoint_is_never_also_plainly_public()
    {
        // [AllowAnonymous] beside [GuestAccess] would make the module filter return before it
        // checks the Guest role's grants, opening the endpoint to everyone regardless of the matrix.
        var both = Actions()
            .Where(a => a.Action.GetCustomAttribute<Sanathana.Companion.Api.Filters.GuestAccessAttribute>() is not null
                     && a.Action.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        Assert.Empty(both);
    }
}

internal static class MethodInfoExtensions
{
    /// <summary>Filters out property getters and setters, which are not actions.</summary>
    public static bool IsSpecialAttribute(this MethodInfo method) => method.IsSpecialName;
}

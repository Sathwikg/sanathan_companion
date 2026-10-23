using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Sanathana.Companion.Api.Controllers;
using Sanathana.Companion.Api.Filters;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.DTOs.Menu;
using Sanathana.Companion.Application.DTOs.Roles;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// "Continue as Guest": a visitor with no token may browse what the Guest role is granted, and
/// nothing that belongs to a person.
/// </summary>
public class GuestAccessTests
{
    // ================================================================== the module filter

    private sealed class FixedCatalog : IAccessRightsCatalog
    {
        private readonly AccessSnapshot _snapshot;

        public FixedCatalog(Dictionary<string, string[]> grants)
            => _snapshot = new AccessSnapshot(grants.ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)g.Value.ToHashSet(StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase));

        public Task<AccessSnapshot> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);

        public void Invalidate() { }
    }

    /// <summary>Guests may read Sadhana; seekers may read Sadhana. Nobody here holds Favorites.</summary>
    private static ModuleAccessFilter Filter(bool guestRoleExists = true)
    {
        var grants = new Dictionary<string, string[]> { [RoleNames.Sanathan] = new[] { ModuleCodes.Sadhana } };
        if (guestRoleExists) grants[RoleNames.Guest] = new[] { ModuleCodes.Sadhana };
        return new ModuleAccessFilter(new FixedCatalog(grants));
    }

    private static AuthorizationFilterContext Request(
        IEnumerable<object> metadata, ClaimsPrincipal? user = null, string? authorizationHeader = null)
    {
        var http = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) };
        if (authorizationHeader is not null) http.Request.Headers.Authorization = authorizationHeader;

        var action = new ActionDescriptor { EndpointMetadata = metadata.ToList() };
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), action), new List<IFilterMetadata>());
    }

    private static ClaimsPrincipal Seeker() => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Role, RoleNames.Sanathan) }, authenticationType: "test"));

    [Fact]
    public async Task A_guest_reads_a_form_the_Guest_role_is_granted()
    {
        var context = Request(new object[] { new RequiresModuleAttribute(ModuleCodes.Sadhana), new GuestAccessAttribute() });

        await Filter().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task A_guest_is_asked_to_sign_in_for_a_form_the_Guest_role_is_not_granted()
    {
        var context = Request(new object[] { new RequiresModuleAttribute(ModuleCodes.Favorites), new GuestAccessAttribute() });

        await Filter().OnAuthorizationAsync(context);

        // 401, not the 403 + X-Access-Denied that would send the client to a signed-in page.
        var result = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Contains(ModuleAccessFilter.GuestDeniedMessage, result.Content);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey(ModuleAccessFilter.DeniedHeader));
    }

    [Fact]
    public async Task Without_a_Guest_role_row_a_guest_reads_nothing()
    {
        // The migration did not run, or someone removed the row: fail closed.
        var context = Request(new object[] { new RequiresModuleAttribute(ModuleCodes.Sadhana), new GuestAccessAttribute() });

        await Filter(guestRoleExists: false).OnAuthorizationAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ContentResult>(context.Result).StatusCode);
    }

    [Fact]
    public async Task A_guest_gets_the_menu_and_the_media_ticket()
    {
        var context = Request(new object[] { new ModuleExemptAttribute(), new GuestAccessAttribute() });

        await Filter().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task A_rejected_token_is_an_ended_session_not_a_guest()
    {
        // The bearer handler could not validate it (expired, say), so the caller arrives anonymous.
        // Serving the guest view would hide the 401 the client renews its token on.
        var context = Request(
            new object[] { new RequiresModuleAttribute(ModuleCodes.Sadhana), new GuestAccessAttribute() },
            authorizationHeader: "Bearer expired.token.value");

        await Filter().OnAuthorizationAsync(context);

        Assert.IsType<ChallengeResult>(context.Result);
    }

    [Fact]
    public async Task A_signed_in_caller_on_a_guest_endpoint_is_checked_as_before()
    {
        var context = Request(
            new object[] { new RequiresModuleAttribute(ModuleCodes.Favorites), new GuestAccessAttribute() },
            Seeker());

        await Filter().OnAuthorizationAsync(context);

        var result = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("module", context.HttpContext.Response.Headers[ModuleAccessFilter.DeniedHeader].ToString());
    }

    [Fact]
    public async Task A_plain_AllowAnonymous_endpoint_stays_public()
    {
        var context = Request(new object[] { new RequiresModuleAttribute(ModuleCodes.Favorites), new AllowAnonymousAttribute() });

        await Filter(guestRoleExists: false).OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    // ================================================================== the menu

    [Fact]
    public async Task A_guest_is_given_the_Guest_roles_menu()
    {
        var service = new Mock<IMenuModuleService>();
        service.Setup(s => s.GetMenuAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<MenuTreeNodeDto>());

        var controller = new MenuModulesController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        await controller.GetMenu(PlatformNames.Mobile, default);

        service.Verify(s => s.GetMenuAsync(PlatformNames.Mobile, RoleNames.Guest, It.IsAny<CancellationToken>()));
    }

    // ================================================================== the localization bundle

    [Fact]
    public async Task A_guest_gets_the_menu_names_translated_and_still_no_other_row_ids()
    {
        var service = new Mock<ILocalizationService>();
        service.Setup(s => s.GetBundleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new LocalizationBundleDto
               {
                   Code = "te",
                   Entities =
                   {
                       ["MenuModule:dddddddd-dddd-dddd-dddd-dddddddddddd:Name"] = "సాధన",
                       ["Deity:11111111-1111-1111-1111-111111111111:Name"] = "వినాయకుడు"
                   }
               });

        var controller = new LocalizationController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var bundle = Assert.IsType<LocalizationBundleDto>(
            Assert.IsType<OkObjectResult>(await controller.GetBundle("te", default)).Value);

        var only = Assert.Single(bundle.Entities);
        Assert.StartsWith(LocalizationController.AnonymousEntityPrefix, only.Key, StringComparison.Ordinal);
    }

    // ================================================================== sadhana

    private sealed class Caller : ICurrentUserService
    {
        public Caller(Guid? userId) => UserId = userId;
        public Guid? UserId { get; }
        public string? Email => null;
        public bool IsAuthenticated => UserId is not null;
    }

    /// <summary>A chant recommended today whatever the weekday: its deity's festival is today.</summary>
    private static async Task<ChantConfig> SeedTodaysChantAsync(TestHarness harness)
    {
        var ctx = harness.Context;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

        ctx.Festivals.Add(new Festival { Id = Guid.NewGuid(), Name = "GuestTestFest", Year = today.Year, Date = today, IsActive = true });
        var deity = new Deity
        {
            Id = Guid.NewGuid(), Name = "GuestTestDeity", DeityType = "God",
            Description = "test deity", Festivals = "GuestTestFest", IsActive = true
        };
        ctx.Deities.Add(deity);
        var category = new Chant { Id = Guid.NewGuid(), Name = "GuestTestCategory", HasCount = true, Count = 108, IsActive = true };
        ctx.Chants.Add(category);
        var cfg = new ChantConfig
        {
            Id = Guid.NewGuid(), ChantId = category.Id, Name = "GuestTestStotram",
            DeityIds = deity.Id.ToString(), ChantText = "<p>om</p>", IsActive = true
        };
        ctx.ChantConfigs.Add(cfg);
        await ctx.SaveChangesAsync();
        return cfg;
    }

    [Fact]
    public async Task A_guest_sees_todays_recommendations_with_no_progress_and_no_streak()
    {
        using var harness = new TestHarness();
        var cfg = await SeedTodaysChantAsync(harness);
        var service = new SadhanaService(harness.UnitOfWork, new Caller(null));

        var today = await service.GetTodayAsync(regionId: null);

        var chant = Assert.Single(today.Recommendations, c => c.Id == cfg.Id);
        Assert.Equal(0, chant.TodayMalas);
        Assert.Empty(today.TodaySessions);
        Assert.Equal(0, today.Streak.CurrentStreak);
        Assert.Equal(0, today.Streak.TotalMalas);
    }

    [Fact]
    public async Task A_guest_can_search_the_chants()
    {
        using var harness = new TestHarness();
        var cfg = await SeedTodaysChantAsync(harness);
        var service = new SadhanaService(harness.UnitOfWork, new Caller(null));

        var chants = await service.GetChantsAsync("GuestTest");

        Assert.Contains(chants, c => c.Id == cfg.Id);
    }

    [Fact]
    public async Task Starting_a_chant_or_counting_one_needs_an_account()
    {
        using var harness = new TestHarness();
        var cfg = await SeedTodaysChantAsync(harness);
        var service = new SadhanaService(harness.UnitOfWork, new Caller(null));

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetChantAsync(cfg.Id));
        await Assert.ThrowsAsync<BadRequestException>(() => service.LogCountAsync(new() { ChantConfigId = cfg.Id, TotalCount = 1 }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetStreakAsync());
    }

    // ================================================================== the role itself

    [Fact]
    public async Task The_Guest_role_cannot_be_renamed_or_deleted()
    {
        using var harness = new TestHarness();
        var guest = new Role { RoleName = RoleNames.Guest, Description = "Visitors" };
        harness.Context.Roles.Add(guest);
        await harness.Context.SaveChangesAsync();

        var service = new RoleService(harness.UnitOfWork, new CreateRoleValidator(), new UpdateRoleValidator());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(guest.RoleId, new UpdateRoleDto { RoleName = "Visitor" }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(guest.RoleId));

        var dto = await service.GetByIdAsync(guest.RoleId);
        Assert.True(dto!.IsSystemRole);
    }
}

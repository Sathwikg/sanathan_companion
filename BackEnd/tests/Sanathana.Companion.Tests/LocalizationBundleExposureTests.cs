using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Api.Controllers;
using Moq;
using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The anonymous bundle used to hand out the primary key of every translated row.
/// </summary>
/// <remarks>
/// Entity keys are "EntityType:EntityKey:Field", and EntityKey is the row's id. Deity names are
/// harvested into that map, so two anonymous requests — locales, then bundle/te — produced the
/// GUID of every translated deity, which is exactly the argument deities/{id}/image takes. The
/// login screen needs the labels and nothing else.
/// </remarks>
public class LocalizationBundleExposureTests
{
    private static LocalizationController Controller(bool signedIn)
    {
        // Moq rather than a hand-written stub: ILocalizationService carries the whole admin
        // editing surface, and none of it is reached here.
        var service = new Mock<ILocalizationService>();
        service.Setup(s => s.GetBundleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((string code, CancellationToken _) => new LocalizationBundleDto
               {
                   Code = code,
                   Labels = { ["auth.signIn"] = "Sign in" },
                   Entities = { ["Deity:11111111-1111-1111-1111-111111111111:Name"] = "వినాయకుడు" }
               });

        var identity = signedIn ? new ClaimsIdentity(authenticationType: "test") : new ClaimsIdentity();

        return new LocalizationController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static LocalizationBundleDto BundleFrom(IActionResult result)
        => Assert.IsType<LocalizationBundleDto>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public async Task An_anonymous_caller_gets_the_labels_and_no_row_identifiers()
    {
        var bundle = BundleFrom(await Controller(signedIn: false).GetBundle("te", default));

        Assert.Equal("Sign in", bundle.Labels["auth.signIn"]);
        Assert.Empty(bundle.Entities);
    }

    [Fact]
    public async Task A_signed_in_caller_still_gets_the_entity_translations()
    {
        // Every screen that renders translated database content is behind [Authorize], so this is
        // the only caller that ever needed them.
        var bundle = BundleFrom(await Controller(signedIn: true).GetBundle("te", default));

        Assert.Single(bundle.Entities);
        Assert.Contains(bundle.Entities.Keys, k => k.StartsWith("Deity:", StringComparison.Ordinal));
    }
}

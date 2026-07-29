using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

public class FavoritesServiceTests
{
    private static (Guid chantConfigId, Guid deityId) Seed(TestHarness harness)
    {
        var ctx = harness.Context;
        var deity = new Deity { Id = Guid.NewGuid(), Name = "Test God", DeityType = "God", IsActive = true };
        var cat = new Chant { Id = Guid.NewGuid(), Name = "TestCat", IsActive = true };
        var cfg = new ChantConfig
        {
            Id = Guid.NewGuid(), ChantId = cat.Id, Name = "Test Chant",
            ChantText = "<p>x</p>", DeityIds = deity.Id.ToString(), IsActive = true
        };
        ctx.Deities.Add(deity);
        ctx.Chants.Add(cat);
        ctx.ChantConfigs.Add(cfg);
        ctx.SaveChanges();
        return (cfg.Id, deity.Id);
    }

    [Fact]
    public async Task Toggle_adds_then_removes_and_reflects_in_ids_and_list()
    {
        using var harness = new TestHarness();
        var (chantId, deityId) = Seed(harness);
        var service = new FavoritesService(harness.UnitOfWork);
        var userId = SeedConstants.AdminUserId;

        Assert.True(await service.ToggleAsync(userId, "Chant", chantId));   // added
        Assert.False(await service.ToggleAsync(userId, "Chant", chantId));  // removed

        await service.ToggleAsync(userId, "chant", chantId);  // case-insensitive add
        await service.ToggleAsync(userId, "Deity", deityId);

        var ids = await service.GetIdsAsync(userId);
        Assert.Contains(chantId, ids.ChantIds);
        Assert.Contains(deityId, ids.DeityIds);

        var favs = await service.GetFavoritesAsync(userId);
        Assert.Contains(favs.Chants, c => c.ChantConfigId == chantId && c.Name == "Test Chant" && c.DeityNames.Contains("Test God"));
        Assert.Contains(favs.Gods, g => g.Id == deityId && g.Name == "Test God");
    }

    [Fact]
    public async Task Toggle_rejects_an_unknown_item()
    {
        using var harness = new TestHarness();
        var service = new FavoritesService(harness.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ToggleAsync(SeedConstants.AdminUserId, "Chant", Guid.NewGuid()));
    }
}

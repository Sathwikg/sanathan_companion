using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.DTOs.Regions;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

public class RegionServiceTests
{
    private static RegionService CreateService(TestHarness harness)
        => new(harness.UnitOfWork, new CreateRegionValidator(), new UpdateRegionValidator());

    [Fact]
    public async Task Create_adds_active_region()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);

        var id = await service.CreateAsync(new CreateRegionDto { Name = "South", Description = "Southern region" });

        var region = await harness.Context.Regions.FirstAsync(r => r.Id == id);
        Assert.Equal("South", region.Name);
        Assert.Equal("Southern region", region.Description);
        Assert.True(region.IsActive);
    }

    [Fact]
    public async Task Create_duplicate_name_throws_Conflict()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);

        await service.CreateAsync(new CreateRegionDto { Name = "North" });

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(new CreateRegionDto { Name = "North" }));
    }

    [Fact]
    public async Task Create_empty_name_throws_Validation()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => service.CreateAsync(new CreateRegionDto { Name = "" }));
    }

    [Fact]
    public async Task SetActive_deactivates_region()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);
        var id = await service.CreateAsync(new CreateRegionDto { Name = "East" });

        await service.SetActiveAsync(id, false);

        var dto = await service.GetByIdAsync(id);
        Assert.NotNull(dto);
        Assert.False(dto!.IsActive);
    }
}

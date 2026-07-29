using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.DTOs.Deities;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

public class DeityServiceTests
{
    // 1x1 transparent PNG data URI
    private const string SampleImage =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static DeityService CreateService(TestHarness harness)
        => new(harness.UnitOfWork, new CreateDeityValidator(), new UpdateDeityValidator());

    [Fact]
    public async Task Create_persists_csv_fields_including_days()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);

        var id = await service.CreateAsync(new CreateDeityDto
        {
            Name = "Ganesha",
            DeityType = "God",
            Description = "Remover of obstacles",
            Regions = new() { "South India", "North India" },
            Festivals = new() { "Ganesh Chaturthi" },
            Days = new() { "Monday", "Wednesday" }
        });

        var dto = await service.GetByIdAsync(id);
        Assert.NotNull(dto);
        Assert.Equal("God", dto!.DeityType);
        Assert.Equal(2, dto.Regions.Count);
        Assert.Contains("Ganesh Chaturthi", dto.Festivals);
        Assert.Equal(new[] { "Monday", "Wednesday" }, dto.Days.ToArray());
        Assert.False(dto.HasImage);

        var entity = await harness.Context.Deities.FirstAsync(d => d.Id == id);
        Assert.Equal("South India,North India", entity.Regions);
        Assert.Equal("Monday,Wednesday", entity.Days);
    }

    [Fact]
    public async Task Update_replaces_days_csv()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);
        var id = await service.CreateAsync(new CreateDeityDto { Name = "Shiva", DeityType = "God", Days = new() { "Monday" } });

        await service.UpdateAsync(id, new UpdateDeityDto { Name = "Shiva", DeityType = "God", Days = new() { "Tuesday", "Thursday" } });

        var dto = await service.GetByIdAsync(id);
        Assert.Equal(new[] { "Tuesday", "Thursday" }, dto!.Days.ToArray());
    }

    [Fact]
    public async Task Create_with_image_stores_blob_and_serves_it()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);

        var id = await service.CreateAsync(new CreateDeityDto { Name = "Krishna", DeityType = "God", ImageBase64 = SampleImage });

        var dto = await service.GetByIdAsync(id);
        Assert.True(dto!.HasImage);

        var (data, contentType) = await service.GetImageAsync(id);
        Assert.NotNull(data);
        Assert.True(data!.Length > 0);
        Assert.Equal("image/png", contentType);
    }

    [Fact]
    public async Task Update_can_remove_image()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);
        var id = await service.CreateAsync(new CreateDeityDto { Name = "Durga", DeityType = "Goddess", ImageBase64 = SampleImage });

        await service.UpdateAsync(id, new UpdateDeityDto { Name = "Durga", DeityType = "Goddess", RemoveImage = true });

        var (data, _) = await service.GetImageAsync(id);
        Assert.Null(data);
    }

    [Fact]
    public async Task Create_duplicate_name_throws_Conflict()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);
        await service.CreateAsync(new CreateDeityDto { Name = "Rama", DeityType = "God" });

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(new CreateDeityDto { Name = "Rama", DeityType = "Goddess" }));
    }

    [Fact]
    public async Task Create_invalid_type_throws_Validation()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => service.CreateAsync(new CreateDeityDto { Name = "X", DeityType = "Alien" }));
    }

    [Fact]
    public async Task FormOptions_returns_seeded_day_names()
    {
        using var harness = new TestHarness();
        var service = CreateService(harness);

        var options = await service.GetFormOptionsAsync();
        Assert.Equal(7, options.Days.Count);
        Assert.Equal("Sunday", options.Days[0]);
    }
}

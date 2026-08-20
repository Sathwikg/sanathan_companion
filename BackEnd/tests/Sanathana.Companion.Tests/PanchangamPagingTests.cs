using Sanathana.Companion.Application.DTOs.Panchangams;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Tests;

/// <summary>
/// GET /api/panchangam used to hand back every row a filter matched. These pin the bound.
/// </summary>
/// <remarks>
/// The <c>search</c> filter is deliberately never exercised here: it is EF.Functions.ILike, which
/// the InMemory provider this harness runs on cannot translate and throws on. Proving that branch
/// needs a real PostgreSQL, which the test project has no client for.
/// </remarks>
public class PanchangamPagingTests
{
    private static async Task<PanchangamService> SeededAsync(TestHarness harness, int rows)
    {
        // The harness seeds roles and the administrator only; regions arrive through migrations,
        // which the InMemory provider never runs.
        var region = new Region { Id = Guid.NewGuid(), Name = "Telangana", Latitude = 17.39, Longitude = 78.49 };
        harness.Context.Regions.Add(region);

        for (var i = 0; i < rows; i++)
        {
            harness.Context.Panchangams.Add(new Panchangam
            {
                Id = Guid.NewGuid(),
                RegionId = region.Id,
                Year = 2026,
                Date = new DateOnly(2026, 1, 1).AddDays(i),
                Masam = "Pushyam"
            });
        }

        await harness.Context.SaveChangesAsync();
        return new PanchangamService(harness.UnitOfWork, new PanchangamComputeCache());
    }

    [Fact]
    public async Task A_page_is_bounded_and_the_total_is_the_whole_match()
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 500);

        var first = await service.GetAllAsync(null, null, null, null, null, page: 1, pageSize: 200);
        Assert.Equal(200, first.Rows.Count);
        Assert.Equal(500, first.TotalCount);

        var last = await service.GetAllAsync(null, null, null, null, null, page: 3, pageSize: 200);
        Assert.Equal(100, last.Rows.Count);
        Assert.Equal(500, last.TotalCount);
    }

    [Fact]
    public async Task Past_the_end_the_answer_is_empty_and_still_truthful_about_the_total()
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 500);

        var beyond = await service.GetAllAsync(null, null, null, null, null, page: 4, pageSize: 200);

        Assert.Empty(beyond.Rows);
        Assert.Equal(500, beyond.TotalCount);
        Assert.Equal(4, beyond.Page);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(10_000, 500)]
    [InlineData(200, 200)]
    public async Task The_page_size_is_clamped_and_the_envelope_says_what_was_used(int asked, int used)
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 10);

        var page = await service.GetAllAsync(null, null, null, null, null, page: 1, pageSize: asked);

        Assert.Equal(used, page.PageSize);
    }

    [Fact]
    public async Task A_page_below_one_is_pulled_up_rather_than_producing_a_negative_skip()
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 10);

        var page = await service.GetAllAsync(null, null, null, null, null, page: 0, pageSize: 5);

        Assert.Equal(1, page.Page);
        Assert.Equal(5, page.Rows.Count);
    }

    [Fact]
    public async Task The_default_call_is_bounded_too()
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 500);

        var page = await service.GetAllAsync(null, null, null, null, null);

        Assert.Equal(200, page.Rows.Count);
    }

    [Fact]
    public async Task Paging_through_everything_yields_each_row_exactly_once()
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 500);

        var seen = new List<Guid>();
        for (var page = 1; page <= 5; page++)
            seen.AddRange((await service.GetAllAsync(null, null, null, null, null, page, 100)).Rows.Select(r => r.Id));

        Assert.Equal(500, seen.Count);
        Assert.Equal(500, seen.Distinct().Count());
    }

    [Fact]
    public async Task The_total_reflects_the_filter_and_not_the_table()
    {
        using var harness = new TestHarness();
        var service = await SeededAsync(harness, 100);

        var week = await service.GetAllAsync(
            null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7), null, pageSize: 500);

        Assert.Equal(7, week.TotalCount);
        Assert.Equal(7, week.Rows.Count);
    }
}

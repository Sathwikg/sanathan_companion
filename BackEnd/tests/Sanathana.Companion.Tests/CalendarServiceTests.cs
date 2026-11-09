using Sanathana.Companion.Application.DTOs.Calendar;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The month view joins four masters that were never joined before. These drive the real service
/// over the in-memory database to pin how they meet: a festival is shown only in its regions and
/// carries the deity that honours it, a weekday carries its deities, a day with no stored
/// Panchangam is computed from the region's coordinates, and a seeker's marks are theirs alone.
/// </summary>
public class CalendarServiceTests
{
    private static readonly Guid Seeker = Guid.Parse("11111111-aaaa-aaaa-aaaa-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-bbbb-bbbb-bbbb-222222222222");

    private static async Task<(TestHarness Harness, CalendarService Service, Region Telangana, Region Kerala)> ArrangeAsync()
    {
        var harness = new TestHarness();
        var ctx = harness.Context;

        var telangana = new Region { Id = Guid.NewGuid(), Name = "Telangana", Latitude = 17.385, Longitude = 78.4867, IsActive = true };
        var kerala = new Region { Id = Guid.NewGuid(), Name = "Kerala", Latitude = 8.5241, Longitude = 76.9366, IsActive = true };
        ctx.Regions.AddRange(telangana, kerala);

        // Observed in Telangana only; the Kerala month must not show it.
        ctx.Festivals.Add(new Festival { Id = Guid.NewGuid(), Name = "Bathukamma", Year = 2026, Date = new DateOnly(2026, 10, 10), Regions = telangana.Id.ToString(), IsActive = true });
        // Everywhere (no regions), but switched off: never shown.
        ctx.Festivals.Add(new Festival { Id = Guid.NewGuid(), Name = "Retired Festival", Year = 2026, Date = new DateOnly(2026, 10, 12), Regions = null, IsActive = false });

        ctx.Deities.Add(new Deity { Id = Guid.NewGuid(), Name = "Gauri", DeityType = "Goddess", Festivals = "Bathukamma, Gowri Habba", Days = "Friday", Regions = "Telangana,Karnataka", IsActive = true, ImageContentType = "image/jpeg" });
        ctx.Deities.Add(new Deity { Id = Guid.NewGuid(), Name = "Hanuman", DeityType = "God", Festivals = "Hanuman Jayanti", Days = "Tuesday,Saturday", Regions = null, IsActive = true });

        ctx.CalendarEvents.Add(new CalendarEvent { Id = Guid.NewGuid(), UserId = Seeker, Date = new DateOnly(2026, 10, 5), Title = "Satyanarayana Vratam", Kind = CalendarEventKinds.Vratam });
        // A yearly repeat that began years ago lands on its anniversary.
        ctx.CalendarEvents.Add(new CalendarEvent { Id = Guid.NewGuid(), UserId = Seeker, Date = new DateOnly(1990, 10, 20), Title = "Amma's Birthday", Kind = CalendarEventKinds.Birthday, RepeatYearly = true });
        // A yearly repeat that begins NEXT year must not appear this year.
        ctx.CalendarEvents.Add(new CalendarEvent { Id = Guid.NewGuid(), UserId = Seeker, Date = new DateOnly(2027, 10, 25), Title = "Future Anniversary", Kind = CalendarEventKinds.Anniversary, RepeatYearly = true });
        ctx.CalendarEvents.Add(new CalendarEvent { Id = Guid.NewGuid(), UserId = Other, Date = new DateOnly(2026, 10, 5), Title = "Somebody else's puja", Kind = CalendarEventKinds.Puja });

        await ctx.SaveChangesAsync();

        var service = new CalendarService(harness.UnitOfWork, new PanchangamComputeCache());
        return (harness, service, telangana, kerala);
    }

    [Fact]
    public async Task A_month_has_every_day_computed_from_the_regions_coordinates_when_nothing_is_stored()
    {
        var (harness, service, telangana, _) = await ArrangeAsync();
        using (harness)
        {
            var month = await service.GetMonthAsync(2026, 10, telangana.Id, null);

            Assert.Equal(31, month.Days.Count);
            Assert.True(month.HasComputedDays);
            Assert.False(month.MissingPanchangam);
            Assert.All(month.Days, d =>
            {
                Assert.True(d.HasPanchangam);
                Assert.NotNull(d.TithiIndex);
                Assert.NotNull(d.NakshatraIndex);
                Assert.NotNull(d.MoonPhase);
                Assert.NotNull(d.Sunrise);
            });
            Assert.NotEmpty(month.Masams);
            Assert.False(string.IsNullOrWhiteSpace(month.Samvatsaram));
            Assert.Equal("Telangana", month.RegionName);
        }
    }

    [Fact]
    public async Task A_festival_shows_only_in_its_regions_and_carries_the_deity_that_honours_it()
    {
        var (harness, service, telangana, kerala) = await ArrangeAsync();
        using (harness)
        {
            var hyderabad = await service.GetMonthAsync(2026, 10, telangana.Id, null);
            var tenth = hyderabad.Days.Single(d => d.Date == new DateOnly(2026, 10, 10));
            var festival = Assert.Single(tenth.Festivals);
            Assert.Equal("Bathukamma", festival.Name);
            var gauri = Assert.Single(festival.Deities);
            Assert.Equal("Gauri", gauri.Name);
            Assert.True(gauri.HasImage);
            Assert.Contains("festival", tenth.Reasons);

            // Switched-off festivals never appear.
            Assert.Empty(hyderabad.Days.Single(d => d.Date == new DateOnly(2026, 10, 12)).Festivals);

            var trivandrum = await service.GetMonthAsync(2026, 10, kerala.Id, null);
            Assert.Empty(trivandrum.Days.Single(d => d.Date == new DateOnly(2026, 10, 10)).Festivals);
        }
    }

    [Fact]
    public async Task A_weekday_carries_the_deities_worshipped_on_it_in_the_region()
    {
        var (harness, service, telangana, kerala) = await ArrangeAsync();
        using (harness)
        {
            var hyderabad = await service.GetMonthAsync(2026, 10, telangana.Id, null);
            var friday = hyderabad.Days.First(d => d.Date.DayOfWeek == DayOfWeek.Friday);
            Assert.Contains(friday.Deities, g => g.Name == "Gauri");
            var tuesday = hyderabad.Days.First(d => d.Date.DayOfWeek == DayOfWeek.Tuesday);
            Assert.Contains(tuesday.Deities, g => g.Name == "Hanuman");

            // Gauri is mapped to Telangana and Karnataka; Kerala's Friday does not carry her.
            var trivandrum = await service.GetMonthAsync(2026, 10, kerala.Id, null);
            Assert.DoesNotContain(trivandrum.Days.First(d => d.Date.DayOfWeek == DayOfWeek.Friday).Deities, g => g.Name == "Gauri");
            // Hanuman has no regions, so every region's Tuesday has him.
            Assert.Contains(trivandrum.Days.First(d => d.Date.DayOfWeek == DayOfWeek.Tuesday).Deities, g => g.Name == "Hanuman");
        }
    }

    [Fact]
    public async Task A_seekers_marks_are_theirs_and_yearly_repeats_land_on_their_anniversary()
    {
        var (harness, service, telangana, _) = await ArrangeAsync();
        using (harness)
        {
            var guest = await service.GetMonthAsync(2026, 10, telangana.Id, null);
            Assert.Empty(guest.Events);

            var mine = await service.GetMonthAsync(2026, 10, telangana.Id, Seeker);
            Assert.Equal(2, mine.Events.Count);
            Assert.DoesNotContain(mine.Events, e => e.Title.StartsWith("Somebody"));
            Assert.DoesNotContain(mine.Events, e => e.Title == "Future Anniversary");

            var birthday = mine.Events.Single(e => e.Kind == CalendarEventKinds.Birthday);
            Assert.Equal(new DateOnly(1990, 10, 20), birthday.Date);
            Assert.Equal(new DateOnly(2026, 10, 20), birthday.OccursOn);
        }
    }

    [Fact]
    public async Task A_mark_can_be_added_changed_and_removed_but_only_by_its_owner()
    {
        var (harness, service, _, _) = await ArrangeAsync();
        using (harness)
        {
            var created = await service.CreateEventAsync(Seeker, new SaveCalendarEventDto
            {
                Date = new DateOnly(2026, 11, 1), Title = "  Karthika Deepam  ", Kind = "puja", Notes = " "
            });
            Assert.Equal("Karthika Deepam", created.Title);     // trimmed
            Assert.Equal(CalendarEventKinds.Puja, created.Kind);   // canonical spelling
            Assert.Null(created.Notes);                            // blank notes are no notes

            var updated = await service.UpdateEventAsync(Seeker, created.Id, new SaveCalendarEventDto
            {
                Date = created.Date, Title = "Karthika Deepam", Kind = CalendarEventKinds.Vratam, RepeatYearly = true
            });
            Assert.True(updated.RepeatYearly);

            // Another seeker's id for the same row is simply "not found": never a hint that it exists.
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteEventAsync(Other, created.Id));
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateEventAsync(Other, created.Id, new SaveCalendarEventDto { Date = created.Date, Title = "x", Kind = "Puja" }));

            await service.DeleteEventAsync(Seeker, created.Id);
            Assert.Empty(await service.GetEventsAsync(Seeker, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        }
    }

    [Fact]
    public async Task Bad_input_is_refused_before_it_is_stored()
    {
        var (harness, service, telangana, _) = await ArrangeAsync();
        using (harness)
        {
            await Assert.ThrowsAsync<BadRequestException>(() => service.CreateEventAsync(Seeker, new SaveCalendarEventDto { Date = new DateOnly(2026, 1, 1), Title = "", Kind = "Puja" }));
            await Assert.ThrowsAsync<BadRequestException>(() => service.CreateEventAsync(Seeker, new SaveCalendarEventDto { Date = new DateOnly(2026, 1, 1), Title = "x", Kind = "Picnic" }));
            await Assert.ThrowsAsync<BadRequestException>(() => service.GetMonthAsync(2026, 13, telangana.Id, null));
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetMonthAsync(2026, 1, Guid.NewGuid(), null));
        }
    }
}

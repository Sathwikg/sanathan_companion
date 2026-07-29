using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

public class NotificationServiceTests
{
    private static readonly Guid UserId = SeedConstants.AdminUserId;

    /// <summary>Enables notifications on one seeded form and returns its config id.</summary>
    private static async Task<Guid> EnableAsync(TestHarness h, NotificationService service,
        bool defaultOn = true, bool mandatory = false)
    {
        await service.SaveConfigAsync(new SaveNotificationConfigDto
        {
            Items = new()
            {
                new SaveNotificationModuleDto
                {
                    MenuModuleId = SeedConstants.TodaysSadhanaMenuId,
                    IsEnabled = true,
                    Title = "Sadhana reminder",
                    DefaultEnabledForUsers = defaultOn,
                    IsMandatory = mandatory
                }
            }
        });

        var mine = await service.GetMySettingsAsync(UserId);
        return mine.Items.Single().ConfigId;
    }

    [Fact]
    public async Task Only_admin_enabled_modules_reach_the_user()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);

        // Nothing configured yet.
        Assert.Empty((await service.GetMySettingsAsync(UserId)).Items);

        await EnableAsync(harness, service);

        var mine = await service.GetMySettingsAsync(UserId);
        var item = Assert.Single(mine.Items);
        Assert.Equal("Sadhana reminder", item.Title);
        Assert.True(item.IsEnabled);          // admin default is on
        Assert.True(item.IsActiveNow);
    }

    [Fact]
    public async Task A_user_with_no_preference_follows_the_admin_default()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);

        await EnableAsync(harness, service, defaultOn: false);

        var item = (await service.GetMySettingsAsync(UserId)).Items.Single();
        Assert.False(item.IsEnabled);
        Assert.Equal("Turned off", item.InactiveReason);
    }

    [Fact]
    public async Task Mandatory_types_cannot_be_switched_off()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);
        var configId = await EnableAsync(harness, service, mandatory: true);

        // The client posts IsEnabled=false; the server must ignore it.
        await service.SaveMySettingsAsync(UserId, new SaveMyNotificationSettingsDto
        {
            MasterEnabled = false,
            Items = new() { new SaveMyNotificationItemDto { ConfigId = configId, IsEnabled = false } }
        });

        var mine = await service.GetMySettingsAsync(UserId);
        var item = mine.Items.Single();
        Assert.True(item.IsMandatory);
        Assert.True(item.IsEnabled);
        Assert.True(item.IsActiveNow);   // survives the master pause
    }

    [Fact]
    public async Task Master_switch_pauses_optional_notifications()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);
        var configId = await EnableAsync(harness, service);

        await service.SaveMySettingsAsync(UserId, new SaveMyNotificationSettingsDto
        {
            MasterEnabled = false,
            Items = new() { new SaveMyNotificationItemDto { ConfigId = configId, IsEnabled = true } }
        });

        var item = (await service.GetMySettingsAsync(UserId)).Items.Single();
        Assert.False(item.IsActiveNow);
        Assert.Equal("All notifications paused", item.InactiveReason);
    }

    [Fact]
    public async Task Quiet_hours_spanning_midnight_suppress_everything()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);
        var configId = await EnableAsync(harness, service, mandatory: true);

        // A window covering the whole day, expressed as a wrap (00:00 -> 23:59 next day).
        await service.SaveMySettingsAsync(UserId, new SaveMyNotificationSettingsDto
        {
            MasterEnabled = true,
            QuietHoursEnabled = true,
            QuietFrom = new TimeOnly(0, 0),
            QuietTo = new TimeOnly(23, 59),
            Items = new() { new SaveMyNotificationItemDto { ConfigId = configId, IsEnabled = true } }
        });

        var mine = await service.GetMySettingsAsync(UserId);
        Assert.True(mine.InQuietHoursNow);
        Assert.False(mine.Items.Single().IsActiveNow);
        Assert.Equal("Quiet hours", mine.Items.Single().InactiveReason);
    }

    [Fact]
    public async Task An_incomplete_time_window_is_rejected()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);
        var configId = await EnableAsync(harness, service);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SaveMySettingsAsync(UserId, new SaveMyNotificationSettingsDto
            {
                Items = new() { new SaveMyNotificationItemDto { ConfigId = configId, IsEnabled = true, FromTime = new TimeOnly(6, 0) } }
            }));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SaveMySettingsAsync(UserId, new SaveMyNotificationSettingsDto
            {
                QuietHoursEnabled = true,
                Items = new()
            }));
    }

    [Fact]
    public async Task Disabling_a_type_hides_it_but_keeps_the_users_choice()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);
        var configId = await EnableAsync(harness, service);

        // User opts out.
        await service.SaveMySettingsAsync(UserId, new SaveMyNotificationSettingsDto
        {
            MasterEnabled = true,
            Items = new() { new SaveMyNotificationItemDto { ConfigId = configId, IsEnabled = false } }
        });

        // Admin turns the whole type off, then back on.
        async Task SetAdminEnabled(bool on) => await service.SaveConfigAsync(new SaveNotificationConfigDto
        {
            Items = new()
            {
                new SaveNotificationModuleDto
                {
                    MenuModuleId = SeedConstants.TodaysSadhanaMenuId, IsEnabled = on,
                    Title = "Sadhana reminder", DefaultEnabledForUsers = true
                }
            }
        });

        await SetAdminEnabled(false);
        Assert.Empty((await service.GetMySettingsAsync(UserId)).Items);

        await SetAdminEnabled(true);
        var item = (await service.GetMySettingsAsync(UserId)).Items.Single();
        Assert.False(item.IsEnabled);   // the earlier opt-out survived

        // The admin list also reports the opt-out.
        var config = await service.GetConfigAsync();
        Assert.Equal(1, config.Modules.Single(m => m.MenuModuleId == SeedConstants.TodaysSadhanaMenuId).OptedOutUsers);
    }

    [Fact]
    public async Task Config_lists_only_navigable_forms_not_containers()
    {
        using var harness = new TestHarness();
        var service = new NotificationService(harness.UnitOfWork);

        var config = await service.GetConfigAsync();

        Assert.Contains(config.Modules, m => m.MenuModuleId == SeedConstants.TodaysSadhanaMenuId);
        // "Masters" is a container with children — it can't raise notifications.
        Assert.DoesNotContain(config.Modules, m => m.MenuModuleId == SeedConstants.MastersModuleId);
    }
}

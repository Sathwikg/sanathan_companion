using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The Digital Personal Data Protection Act, 2023 as the app implements it: consent at sign-up
/// (sections 5-6), adults only (section 9), consent that can be withdrawn and is recorded
/// (sections 6(4), 6(10)), the nominee (section 14), and the rights to access and erasure.
/// </summary>
public class DataProtectionTests
{
    private static RegisterRequestDto Registration(bool notice = true, bool adult = true, int version = PrivacyNotice.CurrentVersion) => new()
    {
        FullName = "Lakshmi Devi",
        Email = "lakshmi@example.org",
        MobileNumber = "9876501234",
        Password = "a quiet lamp",
        ConfirmPassword = "a quiet lamp",
        AcceptPrivacyNotice = notice,
        ConfirmAdult = adult,
        PrivacyNoticeVersion = version
    };

    private static async Task<Guid> RegisteredAsync(TestHarness h)
    {
        await h.AuthService.RegisterAsync(Registration());
        return (await h.Context.Users.SingleAsync(u => u.Email == "lakshmi@example.org")).UserId;
    }

    // ------------------------------------------------------------------ sign-up

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task No_account_is_created_without_both_ticks(bool notice, bool adult)
    {
        using var h = new TestHarness();

        await Assert.ThrowsAsync<ValidationException>(() => h.AuthService.RegisterAsync(Registration(notice, adult)));
        Assert.False(await h.Context.Users.AnyAsync(u => u.Email == "lakshmi@example.org"));
    }

    [Fact]
    public async Task An_app_showing_an_older_notice_cannot_consent_to_the_current_one()
    {
        using var h = new TestHarness();
        await Assert.ThrowsAsync<ValidationException>(() => h.AuthService.RegisterAsync(Registration(version: PrivacyNotice.CurrentVersion - 1)));
    }

    [Fact]
    public async Task Sign_up_records_the_consent_and_the_age_confirmation()
    {
        using var h = new TestHarness();
        var id = await RegisteredAsync(h);

        var user = await h.Context.Users.SingleAsync(u => u.UserId == id);
        Assert.Equal(PrivacyNotice.CurrentVersion, user.PrivacyNoticeVersion);
        Assert.NotNull(user.AdultConfirmedAtUtc);
        Assert.False(user.UsageAnalyticsConsent);   // optional consent is off until given

        var record = await h.Context.ConsentRecords.SingleAsync(c => c.UserId == id);
        Assert.Equal(ConsentPurposes.PrivacyNotice, record.Purpose);
        Assert.True(record.Granted);
    }

    // ------------------------------------------------------------------ existing accounts

    [Fact]
    public async Task An_account_from_before_the_notice_is_asked_once_and_then_not_again()
    {
        using var h = new TestHarness();
        var id = await RegisteredAsync(h);
        var user = await h.Context.Users.SingleAsync(u => u.UserId == id);
        user.PrivacyNoticeVersion = null;
        user.AdultConfirmedAtUtc = null;
        await h.Context.SaveChangesAsync();
        var privacy = new PrivacyService(h.UnitOfWork);

        Assert.True((await privacy.GetAsync(id)).NoticeUpdateRequired);

        await Assert.ThrowsAsync<BadRequestException>(() => privacy.AcceptNoticeAsync(id, new AcceptNoticeDto { NoticeVersion = PrivacyNotice.CurrentVersion, ConfirmAdult = false }, "Android"));

        var after = await privacy.AcceptNoticeAsync(id, new AcceptNoticeDto { NoticeVersion = PrivacyNotice.CurrentVersion, ConfirmAdult = true }, "Android");
        Assert.False(after.NoticeUpdateRequired);
        Assert.Equal("Android", after.History.Last().Platform);
    }

    // ------------------------------------------------------------------ optional consent

    [Fact]
    public async Task Usage_analytics_is_opt_in_and_every_change_is_recorded()
    {
        using var h = new TestHarness();
        var id = await RegisteredAsync(h);
        var privacy = new PrivacyService(h.UnitOfWork);

        Assert.False(await privacy.AllowsUsageAnalyticsAsync(id));

        await privacy.SetUsageAnalyticsAsync(id, true, "Web");
        Assert.True(await privacy.AllowsUsageAnalyticsAsync(id));

        await privacy.SetUsageAnalyticsAsync(id, true, "Web");   // no change, no new record
        var withdrawn = await privacy.SetUsageAnalyticsAsync(id, false, "Web");

        Assert.False(withdrawn.UsageAnalytics);
        Assert.Equal(new[] { true, false }, withdrawn.History.Where(c => c.Purpose == ConsentPurposes.UsageAnalytics).Select(c => c.Granted));
    }

    [Fact]
    public async Task An_unknown_seeker_never_counts_as_having_consented()
    {
        using var h = new TestHarness();
        Assert.False(await new PrivacyService(h.UnitOfWork).AllowsUsageAnalyticsAsync(Guid.NewGuid()));
    }

    // ------------------------------------------------------------------ nominee

    [Fact]
    public async Task A_nominee_needs_a_name_and_a_way_to_reach_them_and_can_be_cleared()
    {
        using var h = new TestHarness();
        var id = await RegisteredAsync(h);
        var privacy = new PrivacyService(h.UnitOfWork);

        await Assert.ThrowsAsync<BadRequestException>(() => privacy.SetNomineeAsync(id, new NomineeDto { Name = "Ravi" }));

        var set = await privacy.SetNomineeAsync(id, new NomineeDto { Name = " Ravi ", Relationship = "Son", Contact = "ravi@example.org" });
        Assert.Equal("Ravi", set.NomineeName);

        var cleared = await privacy.SetNomineeAsync(id, new NomineeDto());
        Assert.Null(cleared.NomineeName);
        Assert.Null(cleared.NomineeContact);
    }

    // ------------------------------------------------------------------ access and erasure

    [Fact]
    public async Task The_data_export_includes_consents_and_nominee()
    {
        using var h = new TestHarness();
        var id = await RegisteredAsync(h);
        await new PrivacyService(h.UnitOfWork).SetNomineeAsync(id, new NomineeDto { Name = "Ravi", Contact = "ravi@example.org" });

        var export = await new AccountDataReader(h.Context).ExportAsync(id);

        Assert.Equal("Ravi", export!.Privacy.NomineeName);
        Assert.Contains(export.Privacy.Consents, c => c.Purpose == ConsentPurposes.PrivacyNotice && c.Granted);
    }

    [Fact]
    public async Task Deleting_the_account_erases_the_consent_history_too()
    {
        using var h = new TestHarness();
        var id = await RegisteredAsync(h);

        await h.UnitOfWork.Users.PurgeUserDataAsync(id);
        await h.UnitOfWork.SaveChangesAsync();

        Assert.False(await h.Context.ConsentRecords.AnyAsync(c => c.UserId == id));
    }

    // ------------------------------------------------------------------ retention

    [Fact]
    public void Logs_are_never_kept_less_than_a_year()
        => Assert.Equal(365, LegalRetention.MinimumLogDays);
}

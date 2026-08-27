namespace Sanathana.Companion.Infrastructure.Seed;

/// <summary>
/// Static, deterministic values used for EF Core <c>HasData</c> seeding. These MUST stay
/// constant (fixed Guid, fixed timestamp, pre-computed hash) so migrations don't drift.
/// </summary>
public static class SeedConstants
{
    public const int AdminRoleId = 1;
    public const int SanathanRoleId = 2;

    /// <summary>Defined in Domain, because closing this account is a rule the Application layer enforces.</summary>
    public static readonly Guid AdminUserId = Domain.Common.WellKnownIds.AdminUser;
    public static readonly Guid DashboardModuleId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid MastersModuleId = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ManageModulesMenuId = new("44444444-4444-4444-4444-444444444444");
    public static readonly Guid RegionMasterMenuId = new("55555555-5555-5555-5555-555555555555");
    public static readonly Guid FestivalsMenuId = new("66666666-6666-6666-6666-666666666666");
    public static readonly Guid DeitiesMenuId = new("77777777-7777-7777-7777-777777777777");
    public static readonly Guid ChantsMenuId = new("88888888-8888-8888-8888-888888888888");
    public static readonly Guid ConfigurationModuleId = new("99999999-9999-9999-9999-999999999999");
    public static readonly Guid ChantConfigMenuId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid LanguagesMenuId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid PanchangamMenuId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid SadhanaModuleId = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid TodaysSadhanaMenuId = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public static readonly Guid UsersMenuId = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
    public static readonly Guid AccessRightsMenuId = new("10101010-1010-1010-1010-101010101010");
    public static readonly Guid RoleMasterMenuId = new("20202020-2020-2020-2020-202020202020");
    public static readonly Guid AdminDashboardMenuId = new("30303030-3030-3030-3030-303030303030");
    public static readonly Guid FeedbackModuleId = new("40404040-4040-4040-4040-404040404040");
    public static readonly Guid FeedbackFormMenuId = new("41414141-4141-4141-4141-414141414141");
    public static readonly Guid FeedbackDashboardMenuId = new("42424242-4242-4242-4242-424242424242");
    public static readonly Guid IssueTypesMenuId = new("43434343-4343-4343-4343-434343434343");
    public static readonly Guid FavoritesMenuId = new("50505050-5050-5050-5050-505050505050");
    public static readonly Guid NotificationsModuleId = new("60606060-6060-6060-6060-606060606060");
    public static readonly Guid NotificationConfigMenuId = new("61616161-6161-6161-6161-616161616161");
    public static readonly Guid MyNotificationsMenuId = new("62626262-6262-6262-6262-626262626262");
    public static readonly Guid LanguageConfigsMenuId = new("70707070-7070-7070-7070-707070707070");
    public static readonly Guid WallpaperMasterMenuId = new("80808080-8080-8080-8080-808080808080");
    public static readonly Guid DownloadWallpapersMenuId = new("81818181-8181-8181-8181-818181818181");
    public static readonly Guid PujaMasterMenuId = new("82828282-8282-8282-8282-828282828282");
    public static readonly Guid PujaProcessConfigMenuId = new("83838383-8383-8383-8383-838383838383");
    public static readonly Guid PujaProcessMenuId = new("84848484-8484-8484-8484-848484848484");
    public static readonly Guid AdConfigMenuId = new("90909090-9090-9090-9090-909090909090");
    public static readonly Guid MobileDashboardMenuId = new("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");

    // The six ad formats the Google Mobile Ads SDK serves. Fixed ids so the placement rows that
    // reference them survive a reseed.
    public static readonly Guid AdFormatBannerId = new("a1a1a1a1-0001-0001-0001-000000000001");
    public static readonly Guid AdFormatInterstitialId = new("a1a1a1a1-0001-0001-0001-000000000002");
    public static readonly Guid AdFormatNativeId = new("a1a1a1a1-0001-0001-0001-000000000003");
    public static readonly Guid AdFormatRewardedId = new("a1a1a1a1-0001-0001-0001-000000000004");
    public static readonly Guid AdFormatRewardedInterstitialId = new("a1a1a1a1-0001-0001-0001-000000000005");
    public static readonly Guid AdFormatAppOpenId = new("a1a1a1a1-0001-0001-0001-000000000006");

    public static readonly Guid AdSettingsId = new("a1a1a1a1-0002-0002-0002-000000000001");

    /// <summary>Tamil was added after the original language seed; fixed id keeps migrations stable.</summary>
    public static readonly Guid TamilLanguageId = new("da000000-0000-0000-0000-000000000006");

    // Seeded common feedback issue types.
    public static readonly Guid IssueTypeBugId = new("41000000-0000-0000-0000-000000000001");
    public static readonly Guid IssueTypeContentId = new("41000000-0000-0000-0000-000000000002");
    public static readonly Guid IssueTypeFeatureId = new("41000000-0000-0000-0000-000000000003");
    public static readonly Guid IssueTypePraiseId = new("41000000-0000-0000-0000-000000000004");
    public static readonly Guid IssueTypeOtherId = new("41000000-0000-0000-0000-000000000005");

    // Default access-rights rows granting the Sanathan (seeker) role the devotee-facing forms.
    public static readonly Guid SanathanDashboardAccessId = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid SanathanSadhanaAccessId = new("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid SanathanPanchangamAccessId = new("a0000000-0000-0000-0000-000000000003");
    public static readonly Guid SanathanFeedbackAccessId = new("a0000000-0000-0000-0000-000000000004");
    public static readonly Guid SanathanFavoritesAccessId = new("a0000000-0000-0000-0000-000000000005");
    public static readonly Guid SanathanNotificationsAccessId = new("a0000000-0000-0000-0000-000000000006");
    public static readonly Guid SanathanWallpapersAccessId = new("a0000000-0000-0000-0000-000000000007");
    public static readonly Guid SanathanPujaProcessAccessId = new("a0000000-0000-0000-0000-000000000008");
    public static readonly Guid SanathanMobileDashboardAccessId = new("a0000000-0000-0000-0000-000000000009");

    /// <summary>
    /// The seeded administrator's password hash: a well-formed BCrypt hash of a random value that
    /// was never recorded, so no password verifies against it.
    /// </summary>
    /// <remarks>
    /// This used to be the hash of the literal "admin", seeded into every database and published in
    /// the README of a public repository — one unauthenticated request to full administrator, with
    /// no way to change it because the API had no password endpoint at all.
    /// <para>
    /// The account is now LOCKED at rest. It is opened exactly once, at start-up, from the
    /// <c>Admin__InitialPassword</c> environment variable, and only while the stored hash is still
    /// this sentinel — see <c>AdminAccountBootstrapper</c>. After that it is changed through
    /// <c>POST /api/auth/change-password</c> like any other account.
    /// </para>
    /// <para>
    /// It must stay syntactically valid BCrypt. An empty or malformed value would make
    /// <c>BCrypt.Verify</c> throw rather than return false, turning every sign-in attempt as the
    /// administrator into a 500 instead of a clean rejection.
    /// </para>
    /// </remarks>
    public const string AdminPasswordHash = "$2a$11$3Qm5rGml0DHTfjcYb4qBSuwNwEcQPXNbe5v8oJEt2hqNz0oV.KEwG";

    /// <summary>Fixed seed timestamp — never use DateTime.UtcNow here (it would regenerate migrations).</summary>
    public static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

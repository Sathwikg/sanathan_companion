using System.IdentityModel.Tokens.Jwt;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Audit;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Audit;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The audit trail: what the interceptor records, how the writer survives bad records, how sessions
/// open and close, and how the switches are honoured.
/// </summary>
public class AuditTests
{
    // ================================================================== data-change interceptor

    private sealed class FixedUser : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");
        public string? Email => "admin@example.com";
        public bool IsAuthenticated => true;
    }

    /// <summary>A context with the real interceptor in front of an in-memory database.</summary>
    private sealed class InterceptorRig : IDisposable
    {
        public RecordingAuditQueue Queue { get; } = new();
        public FakeAuditConfigCache Cache { get; } = new();
        public FakeAuditRequestContext Request { get; } = new();
        public ApplicationDbContext Db { get; }

        public InterceptorRig()
        {
            var user = new FixedUser();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"audit_{Guid.NewGuid()}")
                .AddInterceptors(new AuditSaveChangesInterceptor(Queue, Cache, user, Request))
                .Options;
            Db = new ApplicationDbContext(options, user);
        }

        public List<AuditDataLog> Logs => Queue.OfType<AuditDataLog>().ToList();

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task An_insert_is_recorded_with_its_values_key_user_and_endpoint()
    {
        using var rig = new InterceptorRig();
        rig.Request.ModuleCode = ModuleCodes.Regions;

        var region = new Region { Name = "Telangana", Description = "South" };
        rig.Db.Regions.Add(region);
        await rig.Db.SaveChangesAsync();

        var log = Assert.Single(rig.Logs);
        Assert.Equal("INSERT", log.Action);
        Assert.Equal(nameof(Region), log.EntityName);
        Assert.Equal(region.Id.ToString(), log.EntityId);
        Assert.Equal(ModuleCodes.Regions, log.ModuleCode);
        Assert.Equal("admin@example.com", log.UsernameOrEmail);
        Assert.Equal("203.0.113.7", log.IpAddress);
        Assert.Equal("PUT /api/test", log.Endpoint);
        Assert.Contains("\"Name\":\"Telangana\"", log.NewValuesJson);
        Assert.Null(log.OldValuesJson);
        // The row's own stamps are noise next to the log's own who and when.
        Assert.DoesNotContain("CreatedDate", log.ChangedColumns);
    }

    [Fact]
    public async Task An_update_records_only_what_changed_with_before_and_after()
    {
        using var rig = new InterceptorRig();
        var region = new Region { Name = "Old name", Description = "Kept" };
        rig.Db.Regions.Add(region);
        await rig.Db.SaveChangesAsync();
        rig.Queue.Items.Clear();

        region.Name = "New name";
        await rig.Db.SaveChangesAsync();

        var log = Assert.Single(rig.Logs);
        Assert.Equal("UPDATE", log.Action);
        Assert.Equal("Name", log.ChangedColumns);
        Assert.Contains("Old name", log.OldValuesJson);
        Assert.Contains("New name", log.NewValuesJson);
        Assert.DoesNotContain("Kept", log.NewValuesJson);
    }

    [Fact]
    public async Task Indic_text_stays_readable_in_the_json()
    {
        using var rig = new InterceptorRig();
        rig.Db.Regions.Add(new Region { Name = "తెలంగాణ" });
        await rig.Db.SaveChangesAsync();

        Assert.Contains("తెలంగాణ", Assert.Single(rig.Logs).NewValuesJson);
    }

    [Fact]
    public async Task Binary_columns_are_described_rather_than_copied()
    {
        using var rig = new InterceptorRig();
        rig.Db.Wallpapers.Add(new Wallpaper { DeityId = Guid.NewGuid(), ImageData = new byte[300_000] });
        await rig.Db.SaveChangesAsync();

        var json = Assert.Single(rig.Logs).NewValuesJson!;
        Assert.Contains("[binary 300,000 bytes]", json);
        Assert.True(json.Length < 2000, "The image bytes must not be serialised into the audit row.");
    }

    [Fact]
    public async Task Password_hashes_are_redacted_but_the_change_is_still_visible()
    {
        using var rig = new InterceptorRig();
        rig.Db.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Ravi",
            Email = "ravi@example.com",
            MobileNumber = "9876543210",
            PasswordHash = "$2a$11$supersecrethashvalue"
        });
        await rig.Db.SaveChangesAsync();

        var log = Assert.Single(rig.Logs);
        Assert.Contains("PasswordHash", log.ChangedColumns);
        Assert.DoesNotContain("supersecret", log.NewValuesJson);
        Assert.Contains("[redacted]", log.NewValuesJson);
    }

    [Fact]
    public async Task A_form_with_data_auditing_switched_off_is_not_recorded()
    {
        using var rig = new InterceptorRig();
        rig.Request.ModuleCode = ModuleCodes.Regions;
        rig.Cache.DataByModule[ModuleCodes.Regions] = false;

        rig.Db.Regions.Add(new Region { Name = "Quiet" });
        await rig.Db.SaveChangesAsync();

        Assert.Empty(rig.Logs);
    }

    [Fact]
    public async Task Changes_outside_a_request_such_as_seeding_are_not_recorded()
    {
        using var rig = new InterceptorRig();
        rig.Request.IsHttpRequest = false;

        rig.Db.Regions.Add(new Region { Name = "Seeded" });
        await rig.Db.SaveChangesAsync();

        Assert.Empty(rig.Logs);
    }

    [Fact]
    public async Task Changing_the_audit_settings_is_recorded_even_with_auditing_paused()
    {
        using var rig = new InterceptorRig();
        rig.Cache.IsGlobalAuditEnabled = false;

        rig.Db.AuditSettings.Add(new AuditSettings { IsGlobalAuditEnabled = false });
        rig.Db.Regions.Add(new Region { Name = "Changed while paused" });
        await rig.Db.SaveChangesAsync();

        var log = Assert.Single(rig.Logs);
        Assert.Equal(nameof(AuditSettings), log.EntityName);
        Assert.Equal(ModuleCodes.AuditConfig, log.ModuleCode);
    }

    [Fact]
    public async Task The_audit_tables_and_refresh_tokens_never_audit_themselves()
    {
        using var rig = new InterceptorRig();

        rig.Db.ErrorLogs.Add(new ErrorLog { Message = "x" });
        rig.Db.RefreshTokens.Add(new RefreshToken { UserId = Guid.NewGuid(), TokenHash = new string('a', 64) });
        await rig.Db.SaveChangesAsync();

        Assert.Empty(rig.Logs);
    }

    // ================================================================== the batch writer

    private sealed class WriterRig
    {
        public AuditQueue Queue { get; } = new();
        public FakeAuditConfigCache Cache { get; } = new();
        public ServiceProvider Services { get; }
        public AuditBatchProcessor Processor { get; }

        public WriterRig()
        {
            var name = $"writer_{Guid.NewGuid()}";
            Services = new ServiceCollection()
                .AddScoped<ICurrentUserService, FixedUser>()
                .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(name))
                .BuildServiceProvider();
            Processor = new AuditBatchProcessor(Queue, Cache, Services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AuditBatchProcessor>.Instance);
        }

        public T Query<T>(Func<ApplicationDbContext, T> read)
        {
            using var scope = Services.CreateScope();
            return read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }
    }

    [Fact]
    public async Task One_record_that_cannot_be_written_does_not_take_the_rest_of_the_batch_with_it()
    {
        var rig = new WriterRig();
        var sessionId = Guid.NewGuid();

        await rig.Processor.ProcessBatchAsync(new object[]
        {
            new ErrorLog { Message = "before" },
            new AuditUserSession { Id = sessionId, UserId = Guid.NewGuid() },
            new AuditUserSession { Id = sessionId, UserId = Guid.NewGuid() },   // duplicate key: the poison
            new ErrorLog { Message = "after" }
        }, CancellationToken.None);

        Assert.Equal(new[] { "after", "before" },
            rig.Query(db => db.ErrorLogs.Select(e => e.Message).OrderBy(m => m).ToList()));
        Assert.Equal(1, rig.Query(db => db.AuditUserSessions.Count()));
    }

    [Fact]
    public async Task A_session_opened_and_closed_in_one_batch_gets_its_duration()
    {
        var rig = new WriterRig();
        var id = Guid.NewGuid();
        var login = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

        await rig.Processor.ProcessBatchAsync(new object[]
        {
            new AuditUserSession { Id = id, LoginTimeUtc = login },
            new SessionHeartbeat(id, login.AddMinutes(5)),
            new SessionClose(id, AuditExitReasons.ExplicitLogout, login.AddMinutes(10))
        }, CancellationToken.None);

        var session = rig.Query(db => db.AuditUserSessions.Single());
        Assert.Equal(AuditExitReasons.ExplicitLogout, session.ExitReason);
        Assert.Equal(600, session.DurationSeconds);
        Assert.Equal(login.AddMinutes(10), session.LogoutTimeUtc);
    }

    [Fact]
    public async Task A_password_change_closes_every_other_session_of_that_user()
    {
        var rig = new WriterRig();
        var user = Guid.NewGuid();
        var (old1, old2, current, someoneElse) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var at = DateTime.UtcNow;

        await rig.Processor.ProcessBatchAsync(new object[]
        {
            new AuditUserSession { Id = old1, UserId = user, LoginTimeUtc = at.AddHours(-2) },
            new AuditUserSession { Id = old2, UserId = user, LoginTimeUtc = at.AddHours(-1) },
            new AuditUserSession { Id = someoneElse, UserId = Guid.NewGuid(), LoginTimeUtc = at.AddHours(-1) },
            new AuditUserSession { Id = current, UserId = user, LoginTimeUtc = at },
            new UserSessionsClose(user, AuditExitReasons.PasswordChanged, at, current)
        }, CancellationToken.None);

        var byId = rig.Query(db => db.AuditUserSessions.ToDictionary(s => s.Id));
        Assert.Equal(AuditExitReasons.PasswordChanged, byId[old1].ExitReason);
        Assert.Equal(AuditExitReasons.PasswordChanged, byId[old2].ExitReason);
        Assert.Null(byId[current].LogoutTimeUtc);
        Assert.Null(byId[someoneElse].LogoutTimeUtc);
    }

    [Fact]
    public async Task A_page_visit_counts_as_a_heartbeat_for_its_session()
    {
        var rig = new WriterRig();
        var id = Guid.NewGuid();
        var login = DateTime.UtcNow.AddMinutes(-30);
        await rig.Processor.ProcessBatchAsync(new object[] { new AuditUserSession { Id = id, LoginTimeUtc = login, LastHeartbeatUtc = login } }, CancellationToken.None);

        var exitedAt = DateTime.UtcNow;
        await rig.Processor.ProcessBatchAsync(new object[]
        {
            new AuditActivityLog { SessionId = id, ModuleCode = "deities", RoutePath = "/deities", EnteredAtUtc = exitedAt.AddMinutes(-1), ExitedAtUtc = exitedAt }
        }, CancellationToken.None);

        Assert.Equal(exitedAt, rig.Query(db => db.AuditUserSessions.Single().LastHeartbeatUtc));
    }

    [Fact]
    public void Text_is_trimmed_to_the_column_and_nul_characters_are_removed()
    {
        var rig = new WriterRig();
        var error = new ErrorLog
        {
            Message = new string('m', 5000),
            ExceptionType = "Bad\0Type",
            UserAgent = new string('u', 499) + "😀"   // the cut would split the emoji
        };

        rig.Query(db => { AuditColumnFitter.Fit(db, error); return 0; });

        Assert.Equal(2000, error.Message.Length);
        Assert.Equal("BadType", error.ExceptionType);
        Assert.Equal(499, error.UserAgent!.Length);
    }

    // ================================================================== the session sweep

    [Fact]
    public async Task The_sweep_closes_ended_sessions_with_the_reason_their_tokens_give()
    {
        var rig = new WriterRig();
        var now = DateTime.UtcNow;
        var (signedOut, expired, live) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var lastSeen = now.AddDays(-3);

        rig.Query(db =>
        {
            db.AuditUserSessions.AddRange(
                new AuditUserSession { Id = signedOut, LoginTimeUtc = now.AddHours(-5) },
                new AuditUserSession { Id = expired, LoginTimeUtc = now.AddDays(-40), LastHeartbeatUtc = lastSeen },
                new AuditUserSession { Id = live, LoginTimeUtc = now.AddHours(-1) });
            db.RefreshTokens.AddRange(
                new RefreshToken { FamilyId = signedOut, TokenHash = "a", ExpiresAtUtc = now.AddDays(20), RevokedAtUtc = now.AddHours(-2), RevokedReason = "signed-out" },
                new RefreshToken { FamilyId = expired, TokenHash = "b", ExpiresAtUtc = now.AddDays(-10) },
                new RefreshToken { FamilyId = live, TokenHash = "c", ExpiresAtUtc = now.AddDays(29) });
            return db.SaveChanges();
        });

        var sweeper = new AuditMaintenanceService(rig.Cache, rig.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditMaintenanceService>.Instance);
        var closed = await sweeper.SweepSessionsAsync(now, CancellationToken.None);

        Assert.Equal(2, closed);
        var byId = rig.Query(db => db.AuditUserSessions.ToDictionary(s => s.Id));
        Assert.Equal(AuditExitReasons.ExplicitLogout, byId[signedOut].ExitReason);
        Assert.Equal(now.AddHours(-2), byId[signedOut].LogoutTimeUtc);
        Assert.Equal(AuditExitReasons.SessionExpired, byId[expired].ExitReason);
        Assert.Equal(lastSeen, byId[expired].LogoutTimeUtc);   // when it was last seen, not when the token ran out
        Assert.Null(byId[live].LogoutTimeUtc);
    }

    // ================================================================== the config cache

    private static (AuditConfigCache Cache, ServiceProvider Services) SeededCache()
    {
        var name = $"cache_{Guid.NewGuid()}";
        var services = new ServiceCollection()
            .AddScoped<ICurrentUserService, FixedUser>()
            .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(name))
            .BuildServiceProvider();
        using (var scope = services.CreateScope())
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();

        return (new AuditConfigCache(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<AuditConfigCache>.Instance), services);
    }

    [Fact]
    public async Task Routes_resolve_to_their_form_by_longest_match_and_the_root_only_matches_itself()
    {
        var (cache, _) = SeededCache();
        await cache.ReloadAsync();

        Assert.Equal(ModuleCodes.Deities, cache.ResolveRoute("/deities")?.ModuleCode);
        Assert.Equal(ModuleCodes.Deities, cache.ResolveRoute("/Deities/42/edit?tab=1")?.ModuleCode);
        Assert.Equal(ModuleCodes.PujaProcessConfig, cache.ResolveRoute("/puja-process-config")?.ModuleCode);
        Assert.Equal(ModuleCodes.PujaProcess, cache.ResolveRoute("/puja-process")?.ModuleCode);
        Assert.NotNull(cache.ResolveRoute("/"));
        Assert.Null(cache.ResolveRoute("/profile"));
    }

    [Fact]
    public async Task A_reload_picks_up_switches_saved_in_the_database()
    {
        var (cache, services) = SeededCache();
        await cache.ReloadAsync();
        Assert.True(cache.IsDataAuditEnabled(ModuleCodes.Regions));

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AuditSettings.Single().TrackPageNavigation = false;
            var regions = db.MenuModules.Single(m => m.Code == ModuleCodes.Regions);
            db.AuditModuleConfigs.Add(new AuditModuleConfig { MenuModuleId = regions.Id, ModuleCode = ModuleCodes.Regions, IsDataAuditEnabled = false });
            await db.SaveChangesAsync();
        }

        await cache.ReloadAsync();

        Assert.False(cache.IsDataAuditEnabled(ModuleCodes.Regions));
        Assert.True(cache.IsDataAuditEnabled(ModuleCodes.Deities));
        Assert.False(cache.TrackPageNavigation);
        Assert.True(cache.TrackErrorLogs);
    }

    // ================================================================== sessions from sign-in

    private static async Task<AuthResponseDto> SignInAsync(TestHarness harness)
    {
        await harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            FullName = "Ravi Kumar",
            Email = "ravi@example.com",
            MobileNumber = "9876543210",
            Password = "a quiet lamp",
            ConfirmPassword = "a quiet lamp"
        });
        return (await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "ravi@example.com", Password = "a quiet lamp" }))!;
    }

    private static Guid SessionClaim(string token)
        => Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sid).Value);

    [Fact]
    public async Task Signing_in_opens_a_session_whose_id_the_access_token_carries()
    {
        using var harness = new TestHarness();

        var auth = await SignInAsync(harness);

        var session = Assert.Single(harness.AuditQueue.OfType<AuditUserSession>());
        Assert.Equal(SessionClaim(auth.Token), session.Id);
        Assert.Equal(auth.UserId, session.UserId);
        Assert.Equal("203.0.113.7", session.IpAddress);
        Assert.Equal("Web", session.Platform);
    }

    [Fact]
    public async Task Refreshing_keeps_the_session_and_signing_out_closes_it()
    {
        using var harness = new TestHarness();
        var auth = await SignInAsync(harness);
        var sessionId = SessionClaim(auth.Token);

        var refreshed = await harness.AuthService.RefreshAsync(new RefreshRequestDto { RefreshToken = auth.RefreshToken });
        Assert.Equal(sessionId, SessionClaim(refreshed!.Token));
        Assert.Equal(sessionId, Assert.Single(harness.AuditQueue.OfType<SessionHeartbeat>()).SessionId);

        await harness.AuthService.LogoutAsync(new RefreshRequestDto { RefreshToken = refreshed.RefreshToken });

        var close = Assert.Single(harness.AuditQueue.OfType<SessionClose>());
        Assert.Equal(sessionId, close.SessionId);
        Assert.Equal(AuditExitReasons.ExplicitLogout, close.ExitReason);
        Assert.Single(harness.AuditQueue.OfType<AuditUserSession>());   // a refresh is not a new sign-in
    }

    [Fact]
    public async Task Changing_the_password_closes_the_other_sessions_but_not_the_new_one()
    {
        using var harness = new TestHarness();
        var auth = await SignInAsync(harness);

        var changed = await harness.AuthService.ChangePasswordAsync(auth.UserId, new ChangePasswordDto
        {
            CurrentPassword = "a quiet lamp",
            NewPassword = "a brighter lamp",
            ConfirmNewPassword = "a brighter lamp"
        });

        var close = Assert.Single(harness.AuditQueue.OfType<UserSessionsClose>());
        Assert.Equal(AuditExitReasons.PasswordChanged, close.ExitReason);
        Assert.Equal(SessionClaim(changed!.Token), close.ExceptSessionId);
        Assert.Equal(2, harness.AuditQueue.OfType<AuditUserSession>().Count());
    }

    [Fact]
    public async Task No_session_is_opened_while_session_tracking_is_off()
    {
        using var harness = new TestHarness();
        harness.AuditCache.Sessions = false;

        await SignInAsync(harness);

        Assert.Empty(harness.AuditQueue.OfType<AuditUserSession>());
    }

    [Fact]
    public async Task A_failed_sign_in_records_nothing()
    {
        using var harness = new TestHarness();
        await SignInAsync(harness);
        harness.AuditQueue.Items.Clear();

        var result = await harness.AuthService.LoginAsync(new LoginRequestDto { Credential = "ravi@example.com", Password = "wrong" });

        Assert.Null(result);
        Assert.Empty(harness.AuditQueue.Items);
    }

    // ================================================================== the audit service

    private static AuditService Service(TestHarness harness) => new(
        harness.UnitOfWork, harness.AuditCache, harness.AuditQueue,
        new SaveAuditConfigValidator(), new LogActivityRequestValidator(), new LogErrorRequestValidator());

    private static readonly AuditCaller Caller = new(Guid.NewGuid(), "seeker@example.com", Guid.NewGuid(), "198.51.100.1", "ua", "Android");

    [Fact]
    public async Task A_page_visit_is_credited_to_its_form_and_dated_by_the_server()
    {
        using var harness = new TestHarness();
        harness.AuditCache.Routes["deities"] = new AuditRoute(ModuleCodes.Deities, "Deities");

        var before = DateTime.UtcNow;
        await Service(harness).RecordActivityAsync(new LogActivityRequestDto { RoutePath = "/deities", TimeSpentSeconds = 90 }, Caller);

        var visit = Assert.Single(harness.AuditQueue.OfType<AuditActivityLog>());
        Assert.Equal(ModuleCodes.Deities, visit.ModuleCode);
        Assert.Equal("Deities", visit.FormName);
        Assert.Equal(Caller.SessionId, visit.SessionId);
        Assert.Equal(90, visit.TimeSpentSeconds);
        Assert.True(visit.ExitedAtUtc >= before);
        Assert.Equal(TimeSpan.FromSeconds(90), visit.ExitedAtUtc!.Value - visit.EnteredAtUtc);
    }

    [Fact]
    public async Task An_absurd_duration_is_clamped_to_a_day()
    {
        using var harness = new TestHarness();

        await Service(harness).RecordActivityAsync(new LogActivityRequestDto { RoutePath = "/profile", TimeSpentSeconds = int.MaxValue }, Caller);

        var visit = Assert.Single(harness.AuditQueue.OfType<AuditActivityLog>());
        Assert.Equal(AuditLimits.MaxTimeSpentSeconds, visit.TimeSpentSeconds);
        Assert.Equal("profile", visit.ModuleCode);
    }

    [Fact]
    public async Task A_visit_to_a_form_with_activity_auditing_off_is_dropped()
    {
        using var harness = new TestHarness();
        harness.AuditCache.Routes["deities"] = new AuditRoute(ModuleCodes.Deities, "Deities");
        harness.AuditCache.ActivityByModule[ModuleCodes.Deities] = false;

        await Service(harness).RecordActivityAsync(new LogActivityRequestDto { RoutePath = "/deities", TimeSpentSeconds = 5 }, Caller);

        Assert.Empty(harness.AuditQueue.Items);
    }

    [Fact]
    public async Task A_client_may_not_report_an_error_as_coming_from_the_server()
    {
        using var harness = new TestHarness();

        await Assert.ThrowsAsync<ValidationException>(() => Service(harness).RecordErrorAsync(
            new LogErrorRequestDto { Source = AuditErrorSources.BackendApi, Message = "forged" }, Caller));
        Assert.Empty(harness.AuditQueue.Items);
    }

    [Fact]
    public async Task An_oversized_crash_report_is_trimmed_not_refused()
    {
        using var harness = new TestHarness();

        await Service(harness).RecordErrorAsync(new LogErrorRequestDto
        {
            Source = AuditErrorSources.FrontendWeb,
            Severity = AuditSeverities.Error,
            ExceptionType = "NullReferenceException",
            Message = new string('m', 10_000),
            StackTrace = new string('s', 100_000)
        }, Caller);

        var error = Assert.Single(harness.AuditQueue.OfType<ErrorLog>());
        Assert.Equal(AuditLimits.MaxMessage, error.Message.Length);
        Assert.Equal(AuditLimits.MaxStackTrace, error.StackTrace!.Length);
    }

    [Theory]
    [InlineData(3, 30)]
    [InlineData(90, 400)]
    [InlineData(1000, 30)]
    public async Task Retention_outside_the_allowed_range_is_refused(int auditDays, int errorDays)
    {
        using var harness = new TestHarness();

        await Assert.ThrowsAsync<ValidationException>(() => Service(harness).SaveConfigAsync(new SaveAuditConfigDto
        {
            Settings = new AuditSettingsDto { AuditRetentionDays = auditDays, ErrorRetentionDays = errorDays }
        }));
    }

    [Fact]
    public async Task Saving_the_configuration_stores_the_form_rules_and_reloads_the_cache()
    {
        using var harness = new TestHarness();
        var service = Service(harness);
        var config = await service.GetConfigAsync();
        var deities = config.Modules.Single(m => m.ModuleCode == ModuleCodes.Deities);

        var saved = await service.SaveConfigAsync(new SaveAuditConfigDto
        {
            Settings = new AuditSettingsDto { TrackPageNavigation = false, AuditRetentionDays = 60, ErrorRetentionDays = 14 },
            Modules = { new SaveAuditModuleDto { MenuModuleId = deities.MenuModuleId, IsActivityAuditEnabled = false, IsDataAuditEnabled = true } }
        });

        Assert.False(saved.Settings.TrackPageNavigation);
        Assert.Equal(60, saved.Settings.AuditRetentionDays);
        Assert.False(saved.Modules.Single(m => m.ModuleCode == ModuleCodes.Deities).IsActivityAuditEnabled);
        Assert.Equal(1, harness.AuditCache.Reloads);
    }

    [Fact]
    public async Task Reading_the_configuration_never_writes()
    {
        using var harness = new TestHarness();
        harness.Context.AuditSettings.RemoveRange(harness.Context.AuditSettings);
        await harness.Context.SaveChangesAsync();

        var config = await Service(harness).GetConfigAsync();

        Assert.True(config.Settings.IsGlobalAuditEnabled);
        Assert.False(harness.Context.ChangeTracker.HasChanges());
        Assert.Empty(harness.Context.AuditSettings);
    }

    [Fact]
    public async Task The_error_list_filters_and_pages()
    {
        using var harness = new TestHarness();
        var now = DateTime.UtcNow;
        for (var i = 0; i < 30; i++)
        {
            harness.Context.ErrorLogs.Add(new ErrorLog
            {
                TimestampUtc = now.AddMinutes(-i),
                Source = i % 2 == 0 ? AuditErrorSources.BackendApi : AuditErrorSources.FrontendWeb,
                Message = i == 7 ? "Disk Quota exceeded" : $"error {i}",
                IsResolved = i < 4
            });
        }
        await harness.Context.SaveChangesAsync();
        var service = Service(harness);

        var page2 = await service.GetErrorsAsync(new AuditLogQueryDto { Page = 2, PageSize = 10 });
        Assert.Equal(30, page2.TotalCount);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal("error 10", page2.Items[0].Message);   // newest first

        var open = await service.GetErrorsAsync(new AuditLogQueryDto { Resolved = false, Source = AuditErrorSources.BackendApi });
        Assert.Equal(13, open.TotalCount);

        var found = await service.GetErrorsAsync(new AuditLogQueryDto { Search = "quota" });
        Assert.Equal("Disk Quota exceeded", Assert.Single(found.Items).Message);
    }

    [Fact]
    public async Task Resolving_an_error_returns_who_resolved_it()
    {
        using var harness = new TestHarness();
        var error = new ErrorLog { Message = "boom" };
        harness.Context.ErrorLogs.Add(error);
        await harness.Context.SaveChangesAsync();

        var resolved = await Service(harness).ResolveErrorAsync(error.Id, "admin@example.com");

        Assert.True(resolved!.IsResolved);
        Assert.Equal("admin@example.com", resolved.ResolvedBy);
        Assert.NotNull(resolved.ResolvedAtUtc);
        Assert.Null(await Service(harness).ResolveErrorAsync(Guid.NewGuid(), "x"));
    }
}

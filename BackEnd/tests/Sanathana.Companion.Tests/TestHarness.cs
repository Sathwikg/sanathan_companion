using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Persistence;
using Sanathana.Companion.Infrastructure.Persistence.Repositories;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Wires the real AuthService over an EF Core InMemory database (seeded via HasData/EnsureCreated),
/// real repositories/UnitOfWork, real BCrypt hasher and JWT service — a close-to-integration setup.
/// </summary>
internal sealed class TestHarness : IDisposable
{
    public ApplicationDbContext Context { get; }
    public IUnitOfWork UnitOfWork { get; }
    public AuthService AuthService { get; }
    public IPasswordHasher Hasher { get; }
    public IJwtTokenService Jwt { get; }

    public TestHarness()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"sc_{Guid.NewGuid()}")
            .Options;

        Context = new ApplicationDbContext(options, new TestCurrentUser());
        Context.Database.EnsureCreated(); // applies HasData seed (roles + admin)

        var users = new UserRepository(Context);
        var roles = new RoleRepository(Context);
        var menuModules = new MenuModuleRepository(Context);
        var regions = new RegionRepository(Context);
        var festivals = new FestivalRepository(Context);
        var days = new DayRepository(Context);
        var deities = new DeityRepository(Context);
        var chants = new ChantRepository(Context);
        var chantConfigs = new ChantConfigRepository(Context);
        var languages = new LanguageRepository(Context);
        var panchangams = new PanchangamRepository(Context);
        var sadhana = new SadhanaRepository(Context);
        var moduleRoleMappings = new ModuleRoleMappingRepository(Context);
        var issueTypes = new IssueTypeRepository(Context);
        var feedbacks = new FeedbackRepository(Context);
        var favorites = new UserFavoriteRepository(Context);
        var notificationConfigs = new NotificationConfigRepository(Context);
        var userNotifications = new UserNotificationRepository(Context);
        var localization = new LocalizationRepository(Context);
        UnitOfWork = new UnitOfWork(Context, users, roles, menuModules, regions, festivals, days, deities, chants, chantConfigs, languages, panchangams, sadhana, moduleRoleMappings, issueTypes, feedbacks, favorites, notificationConfigs, userNotifications, localization);

        Hasher = new BCryptPasswordHasher();
        Jwt = new JwtTokenService(Options.Create(new JwtSettings
        {
            Secret = "sanathana-companion-test-secret-key-0123456789ABCDEF",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpiryMinutes = 60
        }));

        AuthService = new AuthService(UnitOfWork, Hasher, Jwt, new RegisterRequestValidator(), new LoginRequestValidator());
    }

    public void Dispose() => Context.Dispose();

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
    }
}

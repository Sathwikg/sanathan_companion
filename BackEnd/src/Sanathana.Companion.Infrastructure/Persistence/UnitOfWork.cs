using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public IUserRepository Users { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IRoleRepository Roles { get; }
    public IMenuModuleRepository MenuModules { get; }
    public IRegionRepository Regions { get; }
    public IFestivalRepository Festivals { get; }
    public IDayRepository Days { get; }
    public IDeityRepository Deities { get; }
    public IWallpaperRepository Wallpapers { get; }
    public IPujaRepository Pujas { get; }
    public IPujaProcessRepository PujaProcess { get; }
    public IChantRepository Chants { get; }
    public IChantConfigRepository ChantConfigs { get; }
    public ILanguageRepository Languages { get; }
    public IPanchangamRepository Panchangams { get; }
    public ISadhanaRepository Sadhana { get; }
    public IModuleRoleMappingRepository ModuleRoleMappings { get; }
    public IIssueTypeRepository IssueTypes { get; }
    public IFeedbackRepository Feedbacks { get; }
    public IUserFavoriteRepository Favorites { get; }
    public INotificationConfigRepository NotificationConfigs { get; }
    public IUserNotificationRepository UserNotifications { get; }
    public ILocalizationRepository Localization { get; }

    public UnitOfWork(ApplicationDbContext context, IUserRepository users, IRefreshTokenRepository refreshTokens, IRoleRepository roles, IMenuModuleRepository menuModules, IRegionRepository regions, IFestivalRepository festivals, IDayRepository days, IDeityRepository deities, IChantRepository chants, IWallpaperRepository wallpapers, IPujaRepository pujas, IPujaProcessRepository pujaProcess, IChantConfigRepository chantConfigs, ILanguageRepository languages, IPanchangamRepository panchangams, ISadhanaRepository sadhana, IModuleRoleMappingRepository moduleRoleMappings, IIssueTypeRepository issueTypes, IFeedbackRepository feedbacks, IUserFavoriteRepository favorites, INotificationConfigRepository notificationConfigs, IUserNotificationRepository userNotifications, ILocalizationRepository localization)
    {
        _context = context;
        Users = users;
        RefreshTokens = refreshTokens;
        Roles = roles;
        MenuModules = menuModules;
        Regions = regions;
        Festivals = festivals;
        Days = days;
        Deities = deities;
        Wallpapers = wallpapers;
        Pujas = pujas;
        PujaProcess = pujaProcess;
        Chants = chants;
        ChantConfigs = chantConfigs;
        Languages = languages;
        Panchangams = panchangams;
        Sadhana = sadhana;
        ModuleRoleMappings = moduleRoleMappings;
        IssueTypes = issueTypes;
        Feedbacks = feedbacks;
        Favorites = favorites;
        NotificationConfigs = notificationConfigs;
        UserNotifications = userNotifications;
        Localization = localization;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}

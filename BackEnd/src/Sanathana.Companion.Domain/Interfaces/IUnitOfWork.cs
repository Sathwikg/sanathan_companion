namespace Sanathana.Companion.Domain.Interfaces;

/// <summary>Coordinates the repositories and commits their changes in a single transaction.</summary>
public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IMenuModuleRepository MenuModules { get; }
    IRegionRepository Regions { get; }
    IFestivalRepository Festivals { get; }
    IDayRepository Days { get; }
    IDeityRepository Deities { get; }
    IChantRepository Chants { get; }
    IChantConfigRepository ChantConfigs { get; }
    ILanguageRepository Languages { get; }
    IPanchangamRepository Panchangams { get; }
    ISadhanaRepository Sadhana { get; }
    IModuleRoleMappingRepository ModuleRoleMappings { get; }
    ILocalizationRepository Localization { get; }
    IIssueTypeRepository IssueTypes { get; }
    IFeedbackRepository Feedbacks { get; }
    IUserFavoriteRepository Favorites { get; }
    INotificationConfigRepository NotificationConfigs { get; }
    IUserNotificationRepository UserNotifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

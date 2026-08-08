using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;

namespace Sanathana.Companion.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMenuModuleService, MenuModuleService>();
        services.AddScoped<IRegionService, RegionService>();
        services.AddScoped<IFestivalService, FestivalService>();
        services.AddScoped<IDeityService, DeityService>();
        services.AddScoped<IChantService, ChantService>();
        services.AddScoped<IChantConfigService, ChantConfigService>();
        services.AddScoped<ILanguageService, LanguageService>();
        services.AddScoped<IPanchangamService, PanchangamService>();
        services.AddScoped<ISadhanaService, SadhanaService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAccessRightsService, AccessRightsService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        // Singleton on purpose: it caches the compiled matchers so the response filter never
        // touches the database. Invalidated explicitly whenever a translation is saved.
        services.AddSingleton<ITranslationCatalog, TranslationCatalog>();
        services.AddScoped<ITermSeedService, TermSeedService>();
        services.AddScoped<IDictionaryService, DictionaryService>();
        services.AddScoped<ITranslationHarvestService, TranslationHarvestService>();
        // Singleton: the result filter writes into it from every request, the harvester drains it.
        services.AddSingleton<ITranslationMissLog, TranslationMissLog>();
        services.AddScoped<IIssueTypeService, IssueTypeService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IFavoritesService, FavoritesService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}

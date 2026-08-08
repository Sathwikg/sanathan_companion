using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Interfaces;
using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Localization;
using Sanathana.Companion.Infrastructure.Persistence;
using Sanathana.Companion.Infrastructure.Persistence.Repositories;

namespace Sanathana.Companion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IMenuModuleRepository, MenuModuleRepository>();
        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<IFestivalRepository, FestivalRepository>();
        services.AddScoped<IDayRepository, DayRepository>();
        services.AddScoped<IDeityRepository, DeityRepository>();
        services.AddScoped<IChantRepository, ChantRepository>();
        services.AddScoped<IChantConfigRepository, ChantConfigRepository>();
        services.AddScoped<ILanguageRepository, LanguageRepository>();
        services.AddScoped<IPanchangamRepository, PanchangamRepository>();
        services.AddScoped<ISadhanaRepository, SadhanaRepository>();
        services.AddScoped<IModuleRoleMappingRepository, ModuleRoleMappingRepository>();
        services.AddScoped<ILocalizationRepository, LocalizationRepository>();
        services.AddSingleton<ILocalizationSeedSource, EmbeddedLocalizationSeedSource>();
        services.AddSingleton<ITermVocabularySource, EmbeddedTermVocabularySource>();
        services.AddScoped<IVocabularyColumnReader, VocabularyColumnReader>();
        services.AddScoped<IIssueTypeRepository, IssueTypeRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IUserFavoriteRepository, UserFavoriteRepository>();
        services.AddScoped<INotificationConfigRepository, NotificationConfigRepository>();
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}

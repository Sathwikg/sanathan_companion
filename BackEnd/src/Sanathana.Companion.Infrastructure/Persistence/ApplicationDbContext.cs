using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Common;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MenuModule> MenuModules => Set<MenuModule>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Festival> Festivals => Set<Festival>();
    public DbSet<Day> Days => Set<Day>();
    public DbSet<Deity> Deities => Set<Deity>();
    public DbSet<Chant> Chants => Set<Chant>();
    public DbSet<ChantConfig> ChantConfigs => Set<ChantConfig>();
    public DbSet<ChantConfigAudio> ChantConfigAudios => Set<ChantConfigAudio>();
    public DbSet<ChantLanguageConfig> ChantLanguageConfigs => Set<ChantLanguageConfig>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<ModuleRoleMapping> ModuleRoleMappings => Set<ModuleRoleMapping>();
    public DbSet<LocalizationResource> LocalizationResources => Set<LocalizationResource>();
    public DbSet<EntityTranslation> EntityTranslations => Set<EntityTranslation>();
    public DbSet<LanguageFormConfig> LanguageFormConfigs => Set<LanguageFormConfig>();
    public DbSet<TranslationTerm> TranslationTerms => Set<TranslationTerm>();
    public DbSet<TranslationTermText> TranslationTermTexts => Set<TranslationTermText>();
    public DbSet<TranslationSource> TranslationSources => Set<TranslationSource>();
    public DbSet<Panchangam> Panchangams => Set<Panchangam>();
    public DbSet<SadhanaLog> SadhanaLogs => Set<SadhanaLog>();
    public DbSet<SadhanaStreak> SadhanaStreaks => Set<SadhanaStreak>();
    public DbSet<IssueType> IssueTypes => Set<IssueType>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();
    public DbSet<NotificationConfig> NotificationConfigs => Set<NotificationConfig>();
    public DbSet<UserNotificationSetting> UserNotificationSettings => Set<UserNotificationSetting>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    private void StampAudit()
    {
        var now = DateTime.UtcNow;
        var by = _currentUser.UserId?.ToString() ?? "system";

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = now;
                    entry.Entity.CreatedBy = by;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedDate = now;
                    entry.Entity.ModifiedBy = by;
                    entry.Property(nameof(BaseEntity.CreatedDate)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}

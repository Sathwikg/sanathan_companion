using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class MenuModuleConfiguration : IEntityTypeConfiguration<MenuModule>
{
    public void Configure(EntityTypeBuilder<MenuModule> builder)
    {
        builder.ToTable("MenuModules");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(150);
        builder.Property(m => m.Icon).HasMaxLength(100);
        builder.Property(m => m.Description).HasMaxLength(500);
        builder.Property(m => m.RoutePath).HasMaxLength(300);
        builder.Property(m => m.Code).HasMaxLength(60);
        builder.Property(m => m.CreatedBy).HasMaxLength(100);
        builder.Property(m => m.ModifiedBy).HasMaxLength(100);

        builder.HasOne(m => m.Parent)
            .WithMany(m => m.Children)
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ParentId).HasDatabaseName("IX_MenuModules_ParentId");

        // Unique, and Postgres allows many NULLs in a unique index — which is what the container
        // rows are.
        builder.HasIndex(m => m.Code).IsUnique().HasDatabaseName("UX_MenuModules_Code");

        // Seed data: the "Dashboard" main menu, plus a "Masters" main menu that
        // contains the "Modules" management form as a sub-module.
        builder.HasData(
            new MenuModule
            {
                Id = SeedConstants.AdminDashboardMenuId,
                Name = "Admin Dashboard",
                Icon = "📊",
                Description = "Community & sadhana analytics for administrators",
                RoutePath = "/admin-dashboard",
                Code = "adminDashboard",
                DisplayOrder = 1,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.DashboardModuleId,
                Name = "User Dashboard",
                Icon = "🕉️",
                Description = "Your personal sadhana home",
                RoutePath = "/",
                Code = "dashboard",
                DisplayOrder = 2,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.MastersModuleId,
                Name = "Masters",
                Icon = "🗂️",
                Description = "Master data & configuration",
                RoutePath = null,
                DisplayOrder = 3,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.ManageModulesMenuId,
                Name = "Modules",
                Icon = "🧩",
                Description = "Manage modules & sub-modules",
                RoutePath = "/modules",
                Code = "modules",
                DisplayOrder = 1,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.RegionMasterMenuId,
                Name = "Region Master",
                Icon = "🗺️",
                Description = "Manage regions",
                RoutePath = "/regions",
                Code = "regions",
                DisplayOrder = 2,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.FestivalsMenuId,
                Name = "Festivals",
                Icon = "🎉",
                Description = "Manage festivals by year",
                RoutePath = "/festivals",
                Code = "festivals",
                DisplayOrder = 3,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.DeitiesMenuId,
                Name = "Deities",
                Icon = "🛕",
                Description = "Manage deities / gods",
                RoutePath = "/deities",
                Code = "deities",
                DisplayOrder = 4,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.ChantsMenuId,
                Name = "Chants",
                Icon = "📿",
                Description = "Manage chants",
                RoutePath = "/chants",
                Code = "chants",
                DisplayOrder = 5,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.UsersMenuId,
                Name = "Users",
                Icon = "👥",
                Description = "Registered users and their profiles",
                RoutePath = "/users",
                Code = "users",
                DisplayOrder = 7,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.LanguagesMenuId,
                Name = "Languages",
                Icon = "🗣️",
                Description = "Manage languages and their regions",
                RoutePath = "/languages",
                Code = "languages",
                DisplayOrder = 6,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.RoleMasterMenuId,
                Name = "Role Master",
                Icon = "🎭",
                Description = "Manage application roles",
                RoutePath = "/roles",
                Code = "roles",
                DisplayOrder = 8,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.ConfigurationModuleId,
                Name = "Configuration",
                Icon = "⚙️",
                Description = "Application configuration",
                RoutePath = null,
                DisplayOrder = 4,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.ChantConfigMenuId,
                Name = "Chants Config",
                Icon = "📜",
                Description = "Configure chants under each chant category",
                RoutePath = "/chants-config",
                Code = "chantsConfig",
                DisplayOrder = 1,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.ConfigurationModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.PanchangamMenuId,
                Name = "Panchangam",
                Icon = "🗓️",
                Description = "Daily Panchangam by region and location",
                RoutePath = "/panchangam",
                Code = "panchangam",
                DisplayOrder = 2,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.ConfigurationModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.SadhanaModuleId,
                Name = "Sadhana",
                Icon = "🙏",
                Description = "Daily spiritual practice",
                RoutePath = null,
                DisplayOrder = 5,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.PujaProcessConfigMenuId,
                Name = "Puja Process",
                Icon = "📜",
                Description = "Required materials and step-by-step process for each puja",
                RoutePath = "/puja-process-config",
                Code = "pujaProcessConfig",
                DisplayOrder = 6,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.ConfigurationModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.PujaProcessMenuId,
                Name = "Puja Process",
                Icon = "📿",
                Description = "Follow a puja step by step",
                RoutePath = "/puja-process",
                Code = "pujaProcess",
                DisplayOrder = 3,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.SadhanaModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.PujaMasterMenuId,
                Name = "Puja",
                Icon = "🛐",
                Description = "Pujas mapped to festivals",
                RoutePath = "/pujas",
                Code = "pujas",
                DisplayOrder = 10,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.WallpaperMasterMenuId,
                Name = "Wallpaper Master",
                Icon = "🖼️",
                Description = "Upload and manage downloadable wallpapers for each deity",
                RoutePath = "/wallpapers",
                Code = "wallpapers",
                DisplayOrder = 9,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.MastersModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.DownloadWallpapersMenuId,
                Name = "Download Wallpapers",
                Icon = "📱",
                Description = "Browse and download deity wallpapers",
                RoutePath = "/wallpapers-download",
                Code = "wallpapersDownload",
                DisplayOrder = 2,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.SadhanaModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.TodaysSadhanaMenuId,
                Name = "Today's Sadhana",
                Icon = "🪷",
                Description = "Recommended chants for today and your japa practice",
                RoutePath = "/sadhana",
                Code = "sadhana",
                DisplayOrder = 1,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.SadhanaModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.AccessRightsMenuId,
                Name = "Access Rights",
                Icon = "🔐",
                Description = "Manage which forms each role can access on web and mobile",
                RoutePath = "/access-rights",
                Code = "accessRights",
                DisplayOrder = 3,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.ConfigurationModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.FeedbackModuleId,
                Name = "Feedback",
                Icon = "💬",
                Description = "Share feedback and review what seekers have sent",
                RoutePath = null,
                DisplayOrder = 6,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.FeedbackFormMenuId,
                Name = "Feedback Form",
                Icon = "📝",
                Description = "Send feedback, suggestions or report an issue",
                RoutePath = "/feedback",
                Code = "feedback",
                DisplayOrder = 1,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.FeedbackModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.FeedbackDashboardMenuId,
                Name = "Feedback Dashboard",
                Icon = "📊",
                Description = "Review and triage the feedback seekers have sent",
                RoutePath = "/feedback-dashboard",
                Code = "feedbackDashboard",
                DisplayOrder = 2,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.FeedbackModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.IssueTypesMenuId,
                Name = "Issue Types",
                Icon = "🏷️",
                Description = "Manage the feedback issue types",
                RoutePath = "/issue-types",
                Code = "issueTypes",
                DisplayOrder = 3,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.FeedbackModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.FavoritesMenuId,
                Name = "Favorites",
                Icon = "⭐",
                Description = "Your favorite chants and gods",
                RoutePath = "/favorites",
                Code = "favorites",
                DisplayOrder = 7,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.NotificationsModuleId,
                Name = "Notifications",
                Icon = "🔔",
                Description = "Notification configuration and personal preferences",
                RoutePath = null,
                DisplayOrder = 8,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = null,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.AdConfigMenuId,
                Name = "Ad Config",
                Icon = "📢",
                Description = "Choose which forms show ads, and which single format each one shows",
                RoutePath = "/ad-config",
                Code = "adConfig",
                DisplayOrder = 7,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.ConfigurationModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.NotificationConfigMenuId,
                Name = "Notification Config",
                Icon = "🛠️",
                Description = "Choose which modules can send notifications",
                RoutePath = "/notification-config",
                Code = "notificationConfig",
                DisplayOrder = 1,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.NotificationsModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.MyNotificationsMenuId,
                Name = "My Notifications",
                Icon = "🔕",
                Description = "Choose what you get notified about, and when",
                RoutePath = "/my-notifications",
                Code = "myNotifications",
                DisplayOrder = 2,
                IsVisibleInMenu = true,
                ShowInMobile = true,
                IsActive = true,
                ParentId = SeedConstants.NotificationsModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new MenuModule
            {
                Id = SeedConstants.LanguageConfigsMenuId,
                Name = "Language Configs",
                Icon = "🌐",
                Description = "Translate the app and choose which forms use each language",
                RoutePath = "/language-configs",
                Code = "languageConfigs",
                DisplayOrder = 4,
                IsVisibleInMenu = true,
                ShowInMobile = false,
                IsActive = true,
                ParentId = SeedConstants.ConfigurationModuleId,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            });
    }
}

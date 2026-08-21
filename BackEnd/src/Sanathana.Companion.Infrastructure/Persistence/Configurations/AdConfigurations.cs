using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class AdFormatConfiguration : IEntityTypeConfiguration<AdFormat>
{
    public void Configure(EntityTypeBuilder<AdFormat> builder)
    {
        builder.ToTable("AdFormats");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Code).IsRequired().HasMaxLength(40);
        builder.Property(f => f.Name).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Description).HasMaxLength(400);
        builder.Property(f => f.PlacementGuidance).HasMaxLength(600);
        builder.Property(f => f.CreatedBy).HasMaxLength(100);
        builder.Property(f => f.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(f => f.Code).IsUnique().HasDatabaseName("UX_AdFormats_Code");

        // The six formats the Google Mobile Ads SDK serves, with Google's own placement rule for
        // each. The guidance is seeded rather than written into the UI so it stays next to the
        // thing it describes, and so it can be translated like any other stored text.
        builder.HasData(
            new AdFormat
            {
                Id = SeedConstants.AdFormatBannerId,
                Code = "banner",
                Name = "Banner",
                Description = "A rectangular strip that occupies part of the screen and stays while the seeker reads. Refreshes on a timer.",
                PlacementGuidance = "Keep it clear of buttons, menus and anything tappable — adjacent controls are the single biggest cause of accidental clicks, and accidental clicks are what gets ad serving disabled.",
                IsFullScreen = false,
                RequiresUserOptIn = false,
                DisplayOrder = 1,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new AdFormat
            {
                Id = SeedConstants.AdFormatInterstitialId,
                Code = "interstitial",
                Name = "Interstitial",
                Description = "A full page that covers the app until the seeker dismisses it.",
                PlacementGuidance = "Only at a natural break between one piece of content and the next. Never on app launch or exit, never straight after another interstitial, and no more than one per two actions. It must not appear while somebody is concentrating on something.",
                IsFullScreen = true,
                RequiresUserOptIn = false,
                DisplayOrder = 2,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new AdFormat
            {
                Id = SeedConstants.AdFormatNativeId,
                Code = "native",
                Name = "Native",
                Description = "Ad content rendered with the app's own styling, so it sits inside a list or a card like ordinary content.",
                PlacementGuidance = "It has to be labelled as an ad and must not be made to look like something the seeker can act on by mistake.",
                IsFullScreen = false,
                RequiresUserOptIn = false,
                DisplayOrder = 3,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new AdFormat
            {
                Id = SeedConstants.AdFormatRewardedId,
                Code = "rewarded",
                Name = "Rewarded",
                Description = "A video the seeker chooses to watch in exchange for something in return.",
                PlacementGuidance = "The seeker must opt in before it plays, and must be told what they get for it. Nothing they already had may be withheld to make them watch.",
                IsFullScreen = true,
                RequiresUserOptIn = true,
                DisplayOrder = 4,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new AdFormat
            {
                Id = SeedConstants.AdFormatRewardedInterstitialId,
                Code = "rewardedInterstitial",
                Name = "Rewarded Interstitial",
                Description = "A full-page reward ad at a transition. Unlike Rewarded, the seeker does not have to opt in first.",
                PlacementGuidance = "An introductory screen has to announce it and offer a way out before it plays. Same transition rules as an interstitial.",
                IsFullScreen = true,
                RequiresUserOptIn = false,
                DisplayOrder = 5,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new AdFormat
            {
                Id = SeedConstants.AdFormatAppOpenId,
                Code = "appOpen",
                Name = "App Open",
                Description = "Shown over the loading screen when the app is opened or returned to.",
                PlacementGuidance = "This is the only format allowed at app launch — an interstitial there is a policy breach. It belongs on the app itself rather than on any one form.",
                IsFullScreen = true,
                RequiresUserOptIn = false,
                DisplayOrder = 6,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            });
    }
}

public class AdPlacementConfiguration : IEntityTypeConfiguration<AdPlacement>
{
    public void Configure(EntityTypeBuilder<AdPlacement> builder)
    {
        builder.ToTable("AdPlacements");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.AndroidAdUnitId).HasMaxLength(120);
        builder.Property(p => p.IosAdUnitId).HasMaxLength(120);
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.ModifiedBy).HasMaxLength(100);

        // One placement per form. Two rows for one module would make "which format" ambiguous,
        // which is the one thing this table exists to answer.
        builder.HasIndex(p => p.MenuModuleId).IsUnique().HasDatabaseName("UX_AdPlacements_MenuModuleId");

        builder.HasOne(p => p.MenuModule)
            .WithMany()
            .HasForeignKey(p => p.MenuModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: deactivating a format is the supported way to withdraw it. Deleting one out
        // from under a live placement would leave a form configured to show nothing.
        builder.HasOne(p => p.AdFormat)
            .WithMany()
            .HasForeignKey(p => p.AdFormatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdSettingsConfiguration : IEntityTypeConfiguration<AdSettings>
{
    public void Configure(EntityTypeBuilder<AdSettings> builder)
    {
        builder.ToTable("AdSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.AndroidAppId).HasMaxLength(120);
        builder.Property(s => s.IosAppId).HasMaxLength(120);
        builder.Property(s => s.CreatedBy).HasMaxLength(100);
        builder.Property(s => s.ModifiedBy).HasMaxLength(100);

        // Seeded off, with test ads on. Nobody should discover that this feature exists because
        // real ads started appearing in front of seekers.
        builder.HasData(new AdSettings
        {
            Id = SeedConstants.AdSettingsId,
            AdsEnabled = false,
            UseTestAds = true,
            CreatedBy = "system",
            CreatedDate = SeedConstants.SeedTimestamp
        });
    }
}

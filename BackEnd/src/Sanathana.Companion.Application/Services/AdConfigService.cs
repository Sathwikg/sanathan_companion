using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Ads;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class AdConfigService : IAdConfigService
{
    private readonly IUnitOfWork _uow;

    public AdConfigService(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Google's public test ad units, which serve a placeholder ad to anybody.
    /// </summary>
    /// <remarks>
    /// Substituted whenever test mode is on, so a half-configured placement shows a test ad rather
    /// than a real one. Clicking your own live ads is what gets an AdMob account suspended, and the
    /// easiest way for that to happen is somebody testing against production ad units.
    /// These ids are published by Google and are the same for everyone.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> AndroidTestUnits = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["banner"] = "ca-app-pub-3940256099942544/6300978111",
        ["interstitial"] = "ca-app-pub-3940256099942544/1033173712",
        ["native"] = "ca-app-pub-3940256099942544/2247696110",
        ["rewarded"] = "ca-app-pub-3940256099942544/5224354917",
        ["rewardedInterstitial"] = "ca-app-pub-3940256099942544/5354046379",
        ["appOpen"] = "ca-app-pub-3940256099942544/9257395921"
    };

    private static readonly IReadOnlyDictionary<string, string> IosTestUnits = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["banner"] = "ca-app-pub-3940256099942544/2934735716",
        ["interstitial"] = "ca-app-pub-3940256099942544/4411468910",
        ["native"] = "ca-app-pub-3940256099942544/3986624511",
        ["rewarded"] = "ca-app-pub-3940256099942544/1712485313",
        ["rewardedInterstitial"] = "ca-app-pub-3940256099942544/6978759866",
        ["appOpen"] = "ca-app-pub-3940256099942544/5575463023"
    };

    private const string AndroidTestAppId = "ca-app-pub-3940256099942544~3347511713";
    private const string IosTestAppId = "ca-app-pub-3940256099942544~1458002511";

    public async Task<AdConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _uow.Ads.GetSettingsAsync(cancellationToken);
        var formats = await _uow.Ads.GetFormatsAsync(cancellationToken);
        var placements = await _uow.Ads.GetPlacementsAsync(cancellationToken);
        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);

        var byModule = placements.ToDictionary(p => p.MenuModuleId);
        var nameById = modules.ToDictionary(m => m.Id, m => m.Name);

        var dto = new AdConfigDto
        {
            AdsEnabled = settings.AdsEnabled,
            AndroidAppId = settings.AndroidAppId,
            IosAppId = settings.IosAppId,
            UseTestAds = settings.UseTestAds,
            Formats = formats.Select(ToDto).ToList()
        };

        // Only rows that are a form. A container has no screen, so there is nowhere to put an ad.
        foreach (var module in modules.Where(m => m.IsActive && !string.IsNullOrWhiteSpace(m.RoutePath)))
        {
            byModule.TryGetValue(module.Id, out var placement);

            dto.Modules.Add(new AdModuleDto
            {
                MenuModuleId = module.Id,
                ModuleName = module.Name,
                Icon = module.Icon,
                ParentName = module.ParentId is { } parentId && nameById.TryGetValue(parentId, out var parent) ? parent : null,
                IsEnabled = placement?.IsEnabled ?? false,
                AdFormatId = placement?.AdFormatId,
                AndroidAdUnitId = placement?.AndroidAdUnitId,
                IosAdUnitId = placement?.IosAdUnitId
            });
        }

        return dto;
    }

    public async Task SaveConfigAsync(SaveAdConfigDto dto, CancellationToken cancellationToken = default)
    {
        var settings = await _uow.Ads.GetSettingsAsync(cancellationToken);
        settings.AdsEnabled = dto.AdsEnabled;
        settings.AndroidAppId = Trim(dto.AndroidAppId);
        settings.IosAppId = Trim(dto.IosAppId);
        settings.UseTestAds = dto.UseTestAds;

        var formats = (await _uow.Ads.GetFormatsAsync(cancellationToken)).ToDictionary(f => f.Id);
        var existing = (await _uow.Ads.GetPlacementsAsync(cancellationToken)).ToDictionary(p => p.MenuModuleId);

        foreach (var item in dto.Modules)
        {
            // An enabled placement has to name a format, and it has to be one that exists and is
            // still offered. Without this a form could be switched on pointing at nothing, and the
            // failure would surface as a blank space on a phone rather than as an error here.
            if (item.IsEnabled)
            {
                if (item.AdFormatId is not { } formatId || !formats.TryGetValue(formatId, out var format))
                    throw new BadRequestException("Choose an ad format for every form you switch on.");

                if (!format.IsActive)
                    throw new BadRequestException($"The '{format.Name}' format is not available.");
            }

            existing.TryGetValue(item.MenuModuleId, out var placement);

            // Nothing to remember about a form that shows no ad and never had one.
            if (!item.IsEnabled && placement is null) continue;

            if (placement is null)
            {
                placement = new AdPlacement { Id = Guid.NewGuid(), MenuModuleId = item.MenuModuleId };
                await _uow.Ads.AddPlacementAsync(placement, cancellationToken);
            }

            placement.IsEnabled = item.IsEnabled;
            // Cleared when switched off, so re-enabling never silently resurrects a format somebody
            // chose months ago and has forgotten about.
            placement.AdFormatId = item.IsEnabled ? item.AdFormatId : null;
            placement.AndroidAdUnitId = Trim(item.AndroidAdUnitId);
            placement.IosAdUnitId = Trim(item.IosAdUnitId);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdSlotDto> GetSlotAsync(Guid menuModuleId, string? platform, CancellationToken cancellationToken = default)
    {
        var slot = new AdSlotDto { MenuModuleId = menuModuleId, ShowAd = false };

        var settings = await _uow.Ads.GetSettingsAsync(cancellationToken);
        if (!settings.AdsEnabled) return slot;

        var isAndroid = string.Equals(platform, "Android", StringComparison.OrdinalIgnoreCase);
        var isIos = string.Equals(platform, "iOS", StringComparison.OrdinalIgnoreCase);

        // The web build shows no ads: this whole feature is the mobile apps, and answering for an
        // unknown platform would mean guessing which ad unit to hand out.
        if (!isAndroid && !isIos) return slot;

        var placement = await _uow.Ads.GetPlacementForModuleAsync(menuModuleId, cancellationToken);
        if (placement is null || !placement.IsEnabled) return slot;
        if (placement.AdFormat is not { IsActive: true } format) return slot;

        var unit = isAndroid ? placement.AndroidAdUnitId : placement.IosAdUnitId;

        if (settings.UseTestAds)
        {
            var testUnits = isAndroid ? AndroidTestUnits : IosTestUnits;
            testUnits.TryGetValue(format.Code, out unit);
        }

        // A placement with no unit for this platform shows nothing rather than falling back to the
        // other platform's unit, which would be an invalid request to the SDK.
        if (string.IsNullOrWhiteSpace(unit)) return slot;

        slot.ShowAd = true;
        slot.FormatCode = format.Code;
        slot.AdUnitId = unit;
        slot.IsTestAd = settings.UseTestAds;
        slot.AppId = settings.UseTestAds
            ? (isAndroid ? AndroidTestAppId : IosTestAppId)
            : (isAndroid ? settings.AndroidAppId : settings.IosAppId);

        return slot;
    }

    private static AdFormatDto ToDto(AdFormat f) => new()
    {
        Id = f.Id,
        Code = f.Code,
        Name = f.Name,
        Description = f.Description,
        PlacementGuidance = f.PlacementGuidance,
        IsFullScreen = f.IsFullScreen,
        RequiresUserOptIn = f.RequiresUserOptIn,
        DisplayOrder = f.DisplayOrder,
        IsActive = f.IsActive
    };

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

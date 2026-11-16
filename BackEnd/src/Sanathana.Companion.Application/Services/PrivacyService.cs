using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public interface IPrivacyService
{
    Task<PrivacySettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The seeker read the current notice and confirmed they are an adult.</summary>
    Task<PrivacySettingsDto> AcceptNoticeAsync(Guid userId, AcceptNoticeDto dto, string? platform, CancellationToken cancellationToken = default);

    /// <summary>Gives or withdraws the optional consent to record which screens are opened.</summary>
    Task<PrivacySettingsDto> SetUsageAnalyticsAsync(Guid userId, bool enabled, string? platform, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears the nominee (all fields empty clears it).</summary>
    Task<PrivacySettingsDto> SetNomineeAsync(Guid userId, NomineeDto dto, CancellationToken cancellationToken = default);

    /// <summary>Whether this seeker consented to usage analytics. False for unknown users.</summary>
    Task<bool> AllowsUsageAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The seeker's side of the Digital Personal Data Protection Act, 2023: the notice and its consent
/// (sections 5 and 6), the optional consent that can be withdrawn as easily as it was given
/// (section 6(4)), and the nominee (section 14). Every consent given or withdrawn is appended to the
/// consent history, which is what proves it later (section 6(10)).
/// </summary>
public sealed class PrivacyService : IPrivacyService
{
    public const int MaxNomineeName = 150;
    public const int MaxNomineeRelationship = 60;
    public const int MaxNomineeContact = 150;

    private readonly IUnitOfWork _uow;

    public PrivacyService(IUnitOfWork uow) => _uow = uow;

    public async Task<PrivacySettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _uow.Users.GetWithRoleAsync(userId, cancellationToken)
                   ?? throw new NotFoundException("Your account could not be found.");
        var history = await _uow.Users.GetConsentsAsync(userId, cancellationToken);
        return ToDto(user, history);
    }

    public async Task<PrivacySettingsDto> AcceptNoticeAsync(Guid userId, AcceptNoticeDto dto, string? platform, CancellationToken cancellationToken = default)
    {
        if (!dto.ConfirmAdult)
            throw new BadRequestException("Accounts are for people aged 18 or older. Please confirm your age to continue.");
        if (dto.NoticeVersion != PrivacyNotice.CurrentVersion)
            throw new BadRequestException("The Privacy Notice has been updated. Please reload and accept the current version.");

        var user = await TrackedAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;

        user.PrivacyNoticeVersion = PrivacyNotice.CurrentVersion;
        user.PrivacyNoticeAcceptedAtUtc = now;
        user.AdultConfirmedAtUtc ??= now;

        await _uow.Users.AddConsentAsync(new ConsentRecord
        {
            UserId = userId,
            Purpose = ConsentPurposes.PrivacyNotice,
            Granted = true,
            NoticeVersion = PrivacyNotice.CurrentVersion,
            AtUtc = now,
            Platform = Clean(platform, 20)
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task<PrivacySettingsDto> SetUsageAnalyticsAsync(Guid userId, bool enabled, string? platform, CancellationToken cancellationToken = default)
    {
        var user = await TrackedAsync(userId, cancellationToken);

        if (user.UsageAnalyticsConsent != enabled)
        {
            user.UsageAnalyticsConsent = enabled;
            await _uow.Users.AddConsentAsync(new ConsentRecord
            {
                UserId = userId,
                Purpose = ConsentPurposes.UsageAnalytics,
                Granted = enabled,
                NoticeVersion = PrivacyNotice.CurrentVersion,
                AtUtc = DateTime.UtcNow,
                Platform = Clean(platform, 20)
            }, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<PrivacySettingsDto> SetNomineeAsync(Guid userId, NomineeDto dto, CancellationToken cancellationToken = default)
    {
        var name = Clean(dto.Name, int.MaxValue);
        var relationship = Clean(dto.Relationship, int.MaxValue);
        var contact = Clean(dto.Contact, int.MaxValue);

        if (name?.Length > MaxNomineeName || relationship?.Length > MaxNomineeRelationship || contact?.Length > MaxNomineeContact)
            throw new BadRequestException("One of the nominee's details is too long.");

        // A nominee is someone who can be reached: a name alone cannot act for anyone.
        var any = name is not null || relationship is not null || contact is not null;
        if (any && (name is null || contact is null))
            throw new BadRequestException("Please give your nominee's name and an email address or mobile number, or clear all three.");

        var user = await TrackedAsync(userId, cancellationToken);
        user.NomineeName = name;
        user.NomineeRelationship = relationship;
        user.NomineeContact = contact;
        await _uow.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<bool> AllowsUsageAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await _uow.Users.GetWithRoleAsync(userId, cancellationToken))?.UsageAnalyticsConsent ?? false;

    private async Task<User> TrackedAsync(Guid userId, CancellationToken cancellationToken)
        => await _uow.Users.GetTrackedWithRoleAsync(userId, cancellationToken)
           ?? throw new NotFoundException("Your account could not be found.");

    private static PrivacySettingsDto ToDto(User user, IReadOnlyList<ConsentRecord> history) => new()
    {
        AcceptedNoticeVersion = user.PrivacyNoticeVersion,
        CurrentNoticeVersion = PrivacyNotice.CurrentVersion,
        CurrentNoticeEffectiveDate = PrivacyNotice.EffectiveDate,
        NoticeUpdateRequired = (user.PrivacyNoticeVersion ?? 0) < PrivacyNotice.CurrentVersion || user.AdultConfirmedAtUtc is null,
        AdultConfirmed = user.AdultConfirmedAtUtc is not null,
        NoticeAcceptedAtUtc = user.PrivacyNoticeAcceptedAtUtc,
        UsageAnalytics = user.UsageAnalyticsConsent,
        NomineeName = user.NomineeName,
        NomineeRelationship = user.NomineeRelationship,
        NomineeContact = user.NomineeContact,
        History = history.Select(h => new ConsentHistoryItemDto
        {
            Purpose = h.Purpose,
            Granted = h.Granted,
            NoticeVersion = h.NoticeVersion,
            AtUtc = h.AtUtc,
            Platform = h.Platform
        }).ToList()
    };

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

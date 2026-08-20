using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence;

/// <inheritdoc />
public class AccountDataReader : IAccountDataReader
{
    private readonly ApplicationDbContext _context;

    public AccountDataReader(ApplicationDbContext context) => _context = context;

    public async Task<MyDataExportDto?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null) return null;

        var regionName = user.DefaultRegionId is { } regionId
            ? (await _context.Regions.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == regionId, cancellationToken))?.Name
            : null;

        var export = new MyDataExportDto
        {
            ExportedAtUtc = DateTime.UtcNow,
            Account = new MyDataExportDto.AccountExport
            {
                FullName = user.FullName,
                Email = user.Email,
                MobileNumber = user.MobileNumber,
                SeekerName = user.SeekerName,
                Role = user.Role?.RoleName ?? string.Empty,
                DefaultRegion = regionName,
                RegisteredOn = user.CreatedDate
            }
        };

        // SadhanaLog already carries the chant name, so the practice history needs no join.
        export.Sadhana = await _context.SadhanaLogs.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Date)
            .Select(l => new MyDataExportDto.SadhanaEntryExport
            {
                Date = l.Date,
                Chant = l.ChantName,
                MalasCompleted = l.MalasCompleted,
                TotalCount = l.TotalCount
            })
            .ToListAsync(cancellationToken);

        // Favourites are polymorphic (a deity or a chant), so the name is resolved per type rather
        // than joined — exporting a bare GUID would tell the seeker nothing.
        var favorites = await _context.UserFavorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => new { f.FavoriteType, f.ItemId })
            .ToListAsync(cancellationToken);

        if (favorites.Count > 0)
        {
            var ids = favorites.Select(f => f.ItemId).ToList();

            var deityNames = await _context.Deities.AsNoTracking()
                .Where(d => ids.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

            var chantNames = await _context.Chants.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

            export.Favorites = favorites.Select(f => new MyDataExportDto.FavoriteExport
            {
                Type = f.FavoriteType,
                Name = deityNames.TryGetValue(f.ItemId, out var deity) ? deity
                     : chantNames.TryGetValue(f.ItemId, out var chant) ? chant
                     : f.ItemId.ToString()
            }).ToList();
        }

        export.Feedback = await (
            from fb in _context.Feedbacks.AsNoTracking().Where(f => f.UserId == userId)
            join type in _context.IssueTypes.AsNoTracking() on fb.IssueTypeId equals type.Id into t
            from type in t.DefaultIfEmpty()
            orderby fb.CreatedDate descending
            select new MyDataExportDto.FeedbackExport
            {
                SubmittedOn = fb.CreatedDate,
                IssueType = type != null ? type.Name : string.Empty,
                Description = fb.Description,
                Status = fb.Status
            }).ToListAsync(cancellationToken);

        var setting = await _context.UserNotificationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        export.Notifications = new MyDataExportDto.NotificationExport
        {
            MasterEnabled = setting?.MasterEnabled ?? true,
            QuietHoursEnabled = setting?.QuietHoursEnabled ?? false,
            QuietFrom = setting?.QuietFrom,
            QuietTo = setting?.QuietTo,
            Preferences = await (
                from pref in _context.UserNotificationPreferences.AsNoTracking().Where(p => p.UserId == userId)
                join config in _context.NotificationConfigs.AsNoTracking()
                    on pref.NotificationConfigId equals config.Id into cfg
                from config in cfg.DefaultIfEmpty()
                select new MyDataExportDto.PreferenceExport
                {
                    Notification = config != null && config.Title != null ? config.Title : string.Empty,
                    IsEnabled = pref.IsEnabled,
                    FromTime = pref.FromTime,
                    ToTime = pref.ToTime
                }).ToListAsync(cancellationToken)
        };

        return export;
    }
}

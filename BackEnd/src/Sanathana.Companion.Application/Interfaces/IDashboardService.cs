using Sanathana.Companion.Application.DTOs.Dashboard;

namespace Sanathana.Companion.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>Community and sadhana metrics for the administrator dashboard.</summary>
    Task<AdminDashboardDto> GetAdminStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Today's deity/deities (by weekday or festival) and the sadhana configured for each,
    /// for the "Today's Bhakti" component on the user dashboard. Optionally limited to one region.</summary>
    Task<TodayBhaktiDto> GetTodayBhaktiAsync(Guid? regionId = null, CancellationToken cancellationToken = default);

    /// <summary>Time-configured prayers ranked by relevance to the current time, for the dashboard.
    /// Optionally limited to one region.</summary>
    Task<PrayersDto> GetPrayersAsync(Guid? regionId = null, CancellationToken cancellationToken = default);
}

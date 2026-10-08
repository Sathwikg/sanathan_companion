using Sanathana.Companion.Application.DTOs.Audit;

namespace Sanathana.Companion.Application.Interfaces;

public interface IAuditService
{
    Task<AuditConfigResponseDto> GetConfigAsync(CancellationToken ct = default);
    Task<AuditConfigResponseDto> SaveConfigAsync(SaveAuditConfigDto dto, CancellationToken ct = default);

    Task RecordActivityAsync(LogActivityRequestDto dto, AuditCaller caller, CancellationToken ct = default);
    Task RecordErrorAsync(LogErrorRequestDto dto, AuditCaller caller, CancellationToken ct = default);

    Task<PagedResultDto<AuditSessionLogDto>> GetSessionsAsync(AuditLogQueryDto query, CancellationToken ct = default);
    Task<PagedResultDto<AuditActivityLogDto>> GetActivitiesAsync(AuditLogQueryDto query, CancellationToken ct = default);
    Task<PagedResultDto<AuditDataLogDto>> GetDataLogsAsync(AuditLogQueryDto query, CancellationToken ct = default);
    Task<PagedResultDto<ErrorLogDto>> GetErrorsAsync(AuditLogQueryDto query, CancellationToken ct = default);

    /// <summary>Marks an error resolved and returns it as it now stands, or null if there is no such error.</summary>
    Task<ErrorLogDto?> ResolveErrorAsync(Guid errorId, string resolvedBy, CancellationToken ct = default);
}

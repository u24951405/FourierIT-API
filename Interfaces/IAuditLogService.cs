using FourierIT_API.Data;
using FourierIT_API.DTOs;
using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    /// <summary>
    /// Service interface for audit log read and write operations.
    /// </summary>
    public interface IAuditLogService
    {
        Task<IEnumerable<AuditLogDto>> GetAllAuditLogsAsync();
        Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(
            string? userId,
            string? actionCode,
            DateTimeOffset? from,
            DateTimeOffset? to);

        Task<FourierIT_API.DTOs.PagedResult<AuditLogDto>> GetAuditLogsPagedAsync(
            string? userId,
            string? actionCode,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page,
            int pageSize,
            string? query);

        Task<AuditLogDto?> GetAuditLogByIdAsync(int id);
        Task<AuditLogDto> CreateAuditLogAsync(AuditLog auditLog);
    }
}

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
        Task<AuditLogDto?> GetAuditLogByIdAsync(int id);
        Task<AuditLogDto> CreateAuditLogAsync(AuditLog auditLog);
    }
}

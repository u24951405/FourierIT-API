using FourierIT_API.DTOs;

namespace FourierIT_API.Interfaces
{
    /// <summary>
    /// Service interface for audit log read operations.
    /// </summary>
    public interface IAuditLogService
    {
        Task<IEnumerable<AuditLogDto>> GetAllAuditLogsAsync();
        Task<AuditLogDto?> GetAuditLogByIdAsync(int id);
    }
}

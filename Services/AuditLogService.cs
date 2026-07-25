using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;

namespace FourierIT_API.Services
{
    /// <summary>
    /// Mock implementation of IAuditLogService that returns static sample data.
    /// This is read-only and intended for development/testing without a database.
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private static readonly List<AuditLogDto> _mockLogs = new()
        {
            new AuditLogDto
            {
                AuditLogId = 1,
                UserId = "system",
                ActionCode = "DB_BACKUP",
                TimeStamp = DateTimeOffset.UtcNow.AddDays(-1),
                Description = "Automated nightly backup completed.",
                TableAffected = "Backups",
                RecordID = null,
                PreviousBlockHash = null,
                BlockHash = "abc123def4567890"
            },
            new AuditLogDto
            {
                AuditLogId = 2,
                UserId = "user-123",
                ActionCode = "DOC_UPLOAD",
                TimeStamp = DateTimeOffset.UtcNow.AddHours(-6),
                Description = "User uploaded document 'id_document.pdf'.",
                TableAffected = "Documents",
                RecordID = 452,
                PreviousBlockHash = "abc123def4567890",
                BlockHash = "def789abc4561230"
            },
            new AuditLogDto
            {
                AuditLogId = 3,
                UserId = "admin-1",
                ActionCode = "PERM_CHANGE",
                TimeStamp = DateTimeOffset.UtcNow.AddHours(-2),
                Description = "Changed permissions for user user-456: granted DocumentOwner role.",
                TableAffected = "AspNetUserRoles",
                RecordID = null,
                PreviousBlockHash = "def789abc4561230",
                BlockHash = "fedcba9876543210"
            }
        };

        public Task<IEnumerable<AuditLogDto>> GetAllAuditLogsAsync()
        {
            var ordered = _mockLogs.OrderByDescending(l => l.TimeStamp).AsEnumerable();
            return Task.FromResult(ordered);
        }

        public Task<AuditLogDto?> GetAuditLogByIdAsync(int id)
        {
            var found = _mockLogs.FirstOrDefault(l => l.AuditLogId == id);
            return Task.FromResult(found);
        }
    }
}

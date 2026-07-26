using FourierIT_API.Data;
using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Services
{
    /// <summary>
    /// Database-backed implementation of IAuditLogService.
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;

        public AuditLogService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<AuditLogDto>> GetAllAuditLogsAsync()
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimeStamp)
                .Select(a => new AuditLogDto
                {
                    AuditLogId = a.AuditLogId,
                    UserId = a.UserId,
                    ActionCode = a.ActionCode,
                    TimeStamp = a.TimeStamp,
                    Description = a.Description,
                    TableAffected = a.TableAffected,
                    RecordID = a.RecordID,
                    PreviousBlockHash = a.PreviousBlockHash,
                    BlockHash = a.BlockHash
                })
                .ToListAsync();
        }

        public async Task<AuditLogDto?> GetAuditLogByIdAsync(int id)
        {
            var auditLog = await _context.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AuditLogId == id);

            if (auditLog == null)
                return null;

            return new AuditLogDto
            {
                AuditLogId = auditLog.AuditLogId,
                UserId = auditLog.UserId,
                ActionCode = auditLog.ActionCode,
                TimeStamp = auditLog.TimeStamp,
                Description = auditLog.Description,
                TableAffected = auditLog.TableAffected,
                RecordID = auditLog.RecordID,
                PreviousBlockHash = auditLog.PreviousBlockHash,
                BlockHash = auditLog.BlockHash
            };
        }

        public async Task<AuditLogDto> CreateAuditLogAsync(AuditLog auditLog)
        {
            if (auditLog == null) throw new ArgumentNullException(nameof(auditLog));

            auditLog.TimeStamp = auditLog.TimeStamp == default ? DateTimeOffset.UtcNow : auditLog.TimeStamp;

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return new AuditLogDto
            {
                AuditLogId = auditLog.AuditLogId,
                UserId = auditLog.UserId,
                ActionCode = auditLog.ActionCode,
                TimeStamp = auditLog.TimeStamp,
                Description = auditLog.Description,
                TableAffected = auditLog.TableAffected,
                RecordID = auditLog.RecordID,
                PreviousBlockHash = auditLog.PreviousBlockHash,
                BlockHash = auditLog.BlockHash
            };
        }
    }
}

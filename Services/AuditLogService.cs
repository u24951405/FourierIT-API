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

        public Task<IEnumerable<AuditLogDto>> GetAllAuditLogsAsync()
        {
            return GetAuditLogsAsync(null, null, null, null);
        }

        public async Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(
            string? userId,
            string? actionCode,
            DateTimeOffset? from,
            DateTimeOffset? to)
        {
            var query = _context.AuditLogs.AsNoTracking().Include(a => a.User).Include(a => a.Institution).AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(a => a.UserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(actionCode))
            {
                query = query.Where(a => a.ActionCode == actionCode);
            }

            if (from.HasValue)
            {
                query = query.Where(a => a.TimeStamp >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(a => a.TimeStamp <= to.Value);
            }

            return await query
                .OrderByDescending(a => a.TimeStamp)
                .Select(a => new AuditLogDto
                {
                    AuditLogId = a.AuditLogId,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.UserName : null,
                    UserEmail = a.User != null ? a.User.Email : null,
                    InstitutionId = a.InstitutionId,
                    InstitutionName = a.Institution != null ? a.Institution.InstitutionName : null,
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

        public async Task<FourierIT_API.DTOs.PagedResult<AuditLogDto>> GetAuditLogsPagedAsync(
            string? userId,
            string? actionCode,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page,
            int pageSize,
            string? query)
        {
            var q = _context.AuditLogs.AsNoTracking().Include(a => a.User).Include(a => a.Institution).AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                q = q.Where(a => a.UserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(actionCode))
            {
                q = q.Where(a => a.ActionCode == actionCode);
            }

            if (from.HasValue)
            {
                q = q.Where(a => a.TimeStamp >= from.Value);
            }

            if (to.HasValue)
            {
                q = q.Where(a => a.TimeStamp <= to.Value);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var qLower = query.ToLowerInvariant();
                q = q.Where(a => (a.Description ?? string.Empty).ToLower().Contains(qLower)
                                 || a.ActionCode.ToLower().Contains(qLower)
                                 || (a.UserId ?? string.Empty).ToLower().Contains(qLower)
                                 || (a.User != null && ((a.User.UserName ?? string.Empty).ToLower().Contains(qLower)
                                     || (a.User.Email ?? string.Empty).ToLower().Contains(qLower)))
                                 || (a.Institution != null && a.Institution.InstitutionName.ToLower().Contains(qLower)));
            }

            var total = await q.CountAsync();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 1000);

            var items = await q
                .OrderByDescending(a => a.TimeStamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    AuditLogId = a.AuditLogId,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.UserName : null,
                    UserEmail = a.User != null ? a.User.Email : null,
                    InstitutionId = a.InstitutionId,
                    InstitutionName = a.Institution != null ? a.Institution.InstitutionName : null,
                    ActionCode = a.ActionCode,
                    TimeStamp = a.TimeStamp,
                    Description = a.Description,
                    TableAffected = a.TableAffected,
                    RecordID = a.RecordID,
                    PreviousBlockHash = a.PreviousBlockHash,
                    BlockHash = a.BlockHash
                })
                .ToListAsync();

            return new FourierIT_API.DTOs.PagedResult<AuditLogDto>
            {
                Items = items,
                TotalCount = total
            };
        }

        public async Task<AuditLogDto?> GetAuditLogByIdAsync(int id)
        {
            var auditLog = await _context.AuditLogs
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Institution)
                .FirstOrDefaultAsync(a => a.AuditLogId == id);

            if (auditLog == null)
                return null;

            return new AuditLogDto
            {
                AuditLogId = auditLog.AuditLogId,
                UserId = auditLog.UserId,
                UserName = auditLog.User?.UserName,
                UserEmail = auditLog.User?.Email,
                InstitutionId = auditLog.InstitutionId,
                InstitutionName = auditLog.Institution?.InstitutionName,
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
                UserName = auditLog.User?.UserName,
                UserEmail = auditLog.User?.Email,
                InstitutionId = auditLog.InstitutionId,
                InstitutionName = auditLog.Institution?.InstitutionName,
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

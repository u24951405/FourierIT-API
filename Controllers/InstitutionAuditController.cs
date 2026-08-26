using FourierIT_API.DTOs.Audit;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourierIT_API.Controllers
{
    [Route("api/institution")]
    [ApiController]
    public class InstitutionAuditController : ControllerBase
    {
        private readonly ILogger<InstitutionAuditController> _logger;
        private readonly IAuditLogService _auditLogService;

        public InstitutionAuditController(
            ILogger<InstitutionAuditController> logger,
            IAuditLogService auditLogService)
        {
            _logger = logger;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Accept audit log entries from institution portal (fire-and-forget, errors ignored).
        /// This is called by the portal after key events (OTP sent, login, logout, etc.)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("audit")]
        public async Task<IActionResult> LogAuditEvent([FromBody] AuditLogEntry entry)
        {
            if (entry == null)
                return Ok(); // Accept silently even if null

            try
            {
                _logger.LogInformation(
                    "Institution portal audit - Action: {ActionType}, User: {UserId}, Institution: {InstitutionId}, Time: {Timestamp}",
                    entry.ActionType, entry.UserId, entry.InstitutionId, entry.Timestamp);

                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    UserId = string.IsNullOrWhiteSpace(entry.UserId) ? null : entry.UserId,
                    ActionCode = entry.ActionType.ToString(),
                    TimeStamp = DateTimeOffset.TryParse(entry.Timestamp, out var parsed)
                        ? parsed
                        : DateTimeOffset.UtcNow,
                    Description = $"Institution portal audit event {entry.ActionType} for institution {entry.InstitutionId}.",
                    TableAffected = "InstitutionPortal",
                    RecordID = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process audit event");
                // Silently fail - audit must not block operations
            }

            return Ok();
        }
    }
}

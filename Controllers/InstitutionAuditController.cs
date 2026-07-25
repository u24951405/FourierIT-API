using FourierIT_API.DTOs.Audit;
using FourierIT_API.DTOs.Institution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourierIT_API.Controllers
{
    [Route("api/institution")]
    [ApiController]
    public class InstitutionAuditController : ControllerBase
    {
        private readonly ILogger<InstitutionAuditController> _logger;

        public InstitutionAuditController(ILogger<InstitutionAuditController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Accept audit log entries from institution portal (fire-and-forget, errors ignored).
        /// This is called by the portal after key events (OTP sent, login, logout, etc.)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("audit")]
        public IActionResult LogAuditEvent([FromBody] AuditLogEntry entry)
        {
            if (entry == null)
                return Ok(); // Accept silently even if null

            try
            {
                // Fire-and-forget: log it and move on
                // In production, you might queue this to a background service
                _logger.LogInformation(
                    "Institution portal audit - Action: {ActionType}, User: {UserId}, Institution: {InstitutionId}, Time: {Timestamp}",
                    entry.ActionType, entry.UserId, entry.InstitutionId, entry.Timestamp);
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

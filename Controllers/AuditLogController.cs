using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FourierIT_API.Controllers
{
    /// <summary>
    /// Controller exposing read-only audit log endpoints for diagnostics and review.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _service;

        public AuditLogController(IAuditLogService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns all audit log entries in reverse chronological order.
        /// </summary>
        /// <returns>List of audit logs.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AuditLogDto>), 200)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? userId,
            [FromQuery] string? actionCode,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? query = null)
        {
            var pagedResult = await _service.GetAuditLogsPagedAsync(userId, actionCode, from, to, page, pageSize, query);
            return Ok(new { items = pagedResult.Items, totalCount = pagedResult.TotalCount });
        }

        /// <summary>
        /// Returns a single audit log entry by id.
        /// </summary>
        /// <param name="id">Audit log id.</param>
        /// <returns>Audit log entry if found; 404 otherwise.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AuditLogDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            var log = await _service.GetAuditLogByIdAsync(id);
            if (log == null) return NotFound();
            return Ok(log);
        }
    }
}

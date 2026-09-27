using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using System.Security.Claims;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "Backup.Manage")]
    public class BackupController : ControllerBase
    {
        private readonly IBackupService _backupService;
        private readonly ILogger<BackupController> _logger;

        public BackupController(IBackupService backupService, ILogger<BackupController> logger)
        {
            _backupService = backupService;
            _logger = logger;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateBackupRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var result = await _backupService.CreateDatabaseBackupAsync(request);

            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                _logger.LogInformation("Backup creation returned status: {Status}", result.StatusMessage);
            }

            // A failed backup returns 400 so the page shows the reason (in StatusMessage).
            if (!result.Success)
            {
                _logger.LogWarning("Backup creation failed: {Status}", result.StatusMessage);
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var backups = await _backupService.GetAllBackupsAsync();
            return Ok(backups);
        }

        /// <summary>
        /// Restore the local SQL Server database from a historical .bak file stored in Azure Blob Storage.
        /// </summary>
        /// <param name="id">Backup record id.</param>
        /// <returns>Restore result with status and timestamp.</returns>
        /// <response code="200">Restore result returned in body.</response>
        [HttpPost("restore/{id}")]
        [ProducesResponseType(typeof(RestoreResponseDto), 200)]
        public async Task<IActionResult> RestoreDatabase([FromRoute] int id)
        {
            _logger.LogInformation("API called to restore database from backup id {BackupId}", id);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _backupService.RestoreDatabaseAsync(id, userId);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
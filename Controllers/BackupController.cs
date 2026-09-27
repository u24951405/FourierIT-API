using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using System.Security.Claims;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "Backup.Manage")]
    public class BackupController : ControllerBase
    {
        private readonly IBackupService _backupService;
        private readonly BackupJobTracker _jobs;
        private readonly ILogger<BackupController> _logger;

        public BackupController(IBackupService backupService, BackupJobTracker jobs, ILogger<BackupController> logger)
        {
            _backupService = backupService;
            _jobs = jobs;
            _logger = logger;
        }

        /// <summary>
        /// Starts a backup in the background and returns straight away (202); the page then asks <c>status</c>
        /// until it finishes. Only one backup runs at a time (409 while one is running).
        /// </summary>
        [HttpPost("create")]
        public IActionResult Create([FromBody] CreateBackupRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (!_jobs.TryStart(request, out var job))
            {
                return Conflict(new { error = "A backup is already running. Wait for it to finish.", job });
            }

            _logger.LogInformation("Manual backup {JobId} started.", job.JobId);
            return Accepted(job);
        }

        /// <summary>The backup running now, or the last one to finish (null if none since the API started).</summary>
        [HttpGet("status")]
        public IActionResult Status() => Ok(_jobs.Current);

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
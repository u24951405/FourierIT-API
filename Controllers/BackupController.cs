using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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

            if (string.IsNullOrWhiteSpace(result.StatusMessage) || result.StatusMessage.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Backup creation returned status: {Status}", result.StatusMessage);
            }

            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var backups = await _backupService.GetAllBackupsAsync();
            return Ok(backups);
        }
    }
}

using FourierIT_API.DTOs.Compliance;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FourierIT_API.Controllers
{
    /// <summary>
    /// FICA Compliance Management Controller
    /// Enterprise-level compliance checking and reporting
    /// </summary>
    [ApiController]
    [Route("api/compliance")]
    [Authorize]
    public class ComplianceController : ControllerBase
    {
        private readonly IComplianceService _complianceService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ComplianceController> _logger;

        public ComplianceController(
            IComplianceService complianceService,
            UserManager<User> userManager,
            ILogger<ComplianceController> logger)
        {
            _complianceService = complianceService;
            _userManager = userManager;
            _logger = logger;
        }

        // ===== COMPLIANCE CHECKS =====

        /// <summary>
        /// Check compliance for a specific user
        /// </summary>
        [HttpPost("users/{userId}/check")]
        [Authorize(Roles = "Admin,Department Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CheckUserCompliance(string userId, [FromBody] CheckComplianceRequestDto? request = null)
        {
            try
            {
                _logger.LogInformation($"Starting compliance check for user {userId}");

                var status = await _complianceService.CheckUserComplianceAsync(
                    userId, 
                    request?.RunDetailedCheck ?? true);

                return Ok(new
                {
                    success = true,
                    message = $"Compliance check completed for {userId}",
                    data = status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking compliance: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Check compliance for entire department
        /// </summary>
        [HttpPost("departments/{departmentId}/check")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> CheckDepartmentCompliance(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid("You can only check compliance for your own department.");

                var status = await _complianceService.CheckDepartmentComplianceAsync(departmentId);
                return Ok(new { success = true, data = status });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Bulk check compliance for multiple users
        /// </summary>
        [HttpPost("bulk-check")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkCheckCompliance([FromBody] BulkComplianceUpdateDto request)
        {
            try
            {
                var statuses = await _complianceService.BulkCheckComplianceAsync(request.UserIds);
                return Ok(new { success = true, checkedCount = statuses.Count, data = statuses });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== COMPLIANCE DETAILS =====

        /// <summary>
        /// Get detailed compliance information for a user
        /// </summary>
        [HttpGet("users/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserComplianceDetails(string userId)
        {
            try
            {
                // Users can only view their own details unless they're Admin
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !User.IsInRole("Admin"))
                    return Forbid("You can only view your own compliance details");

                var details = await _complianceService.GetUserComplianceDetailsAsync(userId);
                return Ok(new { success = true, data = details });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<bool> UserCanAccessDepartmentAsync(int departmentId)
        {
            if (User.IsInRole("Admin"))
                return true;

            if (!User.IsInRole("Department Admin"))
                return false;

            var currentUser = await GetCurrentUserAsync();
            return currentUser != null && currentUser.DepartmentId == departmentId;
        }

        /// <summary>
        /// Get compliance details for all users in a department
        /// </summary>
        [HttpGet("departments/{departmentId}/users")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> GetDepartmentUserCompliance(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid("You can only view compliance for your own department.");

                var details = await _complianceService.GetDepartmentUserComplianceAsync(departmentId);
                return Ok(new { success = true, count = details.Count, data = details });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== DASHBOARD =====

        /// <summary>
        /// Get system-wide compliance dashboard
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSystemDashboard()
        {
            try
            {
                var dashboard = await _complianceService.GetSystemDashboardAsync();
                return Ok(new { success = true, data = dashboard });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get department compliance dashboard
        /// </summary>
        [HttpGet("departments/{departmentId}/dashboard")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> GetDepartmentDashboard(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid("You can only view the dashboard for your own department.");

                var dashboard = await _complianceService.GetDepartmentDashboardAsync(departmentId);
                return Ok(new { success = true, data = dashboard });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get compliance statistics
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStatistics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var stats = await _complianceService.GetComplianceStatisticsAsync(startDate, endDate);
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== DOCUMENTS =====

        /// <summary>
        /// Get compliance issues for a user's documents
        /// </summary>
        [HttpGet("users/{userId}/issues")]
        [Authorize]
        public async Task<IActionResult> GetDocumentIssues(string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !User.IsInRole("Admin"))
                    return Forbid();

                var issues = await _complianceService.GetDocumentIssuesAsync(userId);
                return Ok(new { success = true, count = issues.Count, data = issues });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get missing documents for a user
        /// </summary>
        [HttpGet("users/{userId}/missing-documents")]
        [Authorize]
        public async Task<IActionResult> GetMissingDocuments(string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !User.IsInRole("Admin"))
                    return Forbid();

                var missing = await _complianceService.IdentifyMissingDocumentsAsync(userId);
                return Ok(new { success = true, count = missing.Count, data = missing });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== ALERTS =====

        /// <summary>
        /// Get open compliance alerts
        /// </summary>
        [HttpGet("alerts")]
        [Authorize]
        public async Task<IActionResult> GetOpenAlerts([FromQuery] string? userId = null)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Users can only view their own alerts unless they're Admin
                if (string.IsNullOrEmpty(userId))
                    userId = currentUserId;
                else if (userId != currentUserId && !User.IsInRole("Admin"))
                    return Forbid();

                var alerts = await _complianceService.GetOpenAlertsAsync(userId);
                return Ok(new { success = true, count = alerts.Count, data = alerts });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get overdue alerts
        /// </summary>
        [HttpGet("alerts/overdue")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetOverdueAlerts()
        {
            try
            {
                var alerts = await _complianceService.GetOverdueAlertsAsync();
                return Ok(new { success = true, count = alerts.Count, data = alerts });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Acknowledge an alert
        /// </summary>
        [HttpPut("alerts/{alertId}/acknowledge")]
        [Authorize]
        public async Task<IActionResult> AcknowledgeAlert(int alertId, [FromBody] AcknowledgeAlertDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await _complianceService.AcknowledgeAlertAsync(alertId, currentUserId, request.AcknowledgmentNotes);

                return Ok(new { success = result, message = "Alert acknowledged" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Resolve an alert
        /// </summary>
        [HttpPut("alerts/{alertId}/resolve")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> ResolveAlert(int alertId, [FromBody] ResolveAlertDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await _complianceService.ResolveAlertAsync(alertId, currentUserId, request.ResolutionNotes);

                return Ok(new { success = result, message = "Alert resolved" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== APPROVALS =====

        /// <summary>
        /// Approve document compliance
        /// </summary>
        [HttpPost("documents/{checkId}/approve")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> ApproveDocumentCompliance(int checkId, [FromBody] ApproveDocumentComplianceDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await _complianceService.ApproveDocumentComplianceAsync(checkId, currentUserId, request.ApprovalNotes);

                return Ok(new { success = result, message = "Document compliance approved" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Reject document compliance
        /// </summary>
        [HttpPost("documents/{checkId}/reject")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> RejectDocumentCompliance(int checkId, [FromBody] ApproveDocumentComplianceDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await _complianceService.RejectDocumentComplianceAsync(checkId, currentUserId, request.ApprovalNotes);

                return Ok(new { success = result, message = "Document compliance rejected" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Bulk approve documents
        /// </summary>
        [HttpPost("documents/bulk-approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkApproveDocuments([FromBody] List<int> checkIds)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var count = await _complianceService.BulkApproveDocumentsAsync(checkIds, currentUserId);

                return Ok(new { success = true, approved = count });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== DEADLINES =====

        /// <summary>
        /// Set compliance deadline for a user
        /// </summary>
        [HttpPost("users/{userId}/deadline")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> SetDeadline(string userId, [FromBody] SetComplianceDeadlineDto request)
        {
            try
            {
                var result = await _complianceService.SetComplianceDeadlineAsync(userId, request.Deadline, request.Reason);
                return Ok(new { success = result, message = "Deadline set successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get users near their compliance deadline
        /// </summary>
        [HttpGet("users/deadline/near")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUsersNearDeadline([FromQuery] int daysThreshold = 7)
        {
            try
            {
                var users = await _complianceService.GetUsersNearDeadlineAsync(daysThreshold);
                return Ok(new { success = true, count = users.Count, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== ESCALATION =====

        /// <summary>
        /// Escalate a user's compliance for manual review
        /// </summary>
        [HttpPost("users/{userId}/escalate")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> EscalateCompliance(string userId, [FromBody] BulkComplianceUpdateDto request)
        {
            try
            {
                var result = await _complianceService.EscalateComplianceAsync(userId, request.Notes ?? "No reason provided");
                return Ok(new { success = result, message = "Compliance escalated for review" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Flag document for manual review
        /// </summary>
        [HttpPost("documents/{checkId}/flag-review")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> FlagForManualReview(int checkId, [FromBody] BulkComplianceUpdateDto request)
        {
            try
            {
                var result = await _complianceService.FlagForManualReviewAsync(checkId, request.Notes ?? "No reason provided");
                return Ok(new { success = result, message = "Document flagged for manual review" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get documents pending manual review
        /// </summary>
        [HttpGet("documents/pending-review")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> GetPendingManualReviews()
        {
            try
            {
                var documents = await _complianceService.GetPendingManualReviewsAsync();
                return Ok(new { success = true, count = documents.Count, data = documents });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== HISTORY & AUDIT =====

        /// <summary>
        /// Get compliance history for a user
        /// </summary>
        [HttpGet("users/{userId}/history")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetComplianceHistory(string userId, [FromQuery] int limit = 50)
        {
            try
            {
                // First get the compliance status for this user
                var details = await _complianceService.GetUserComplianceDetailsAsync(userId);
                if (string.IsNullOrEmpty(details.UserId))
                    return NotFound("User compliance history not found");

                // This is simplified - in real implementation we'd need the status ID
                // For now returning the compliance details which includes history
                return Ok(new { success = true, data = details });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== NOTIFICATIONS =====

        /// <summary>
        /// Send non-compliance notification to user
        /// </summary>
        [HttpPost("users/{userId}/send-notification")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> SendNonComplianceNotification(string userId)
        {
            try
            {
                var result = await _complianceService.SendNonComplianceNotificationAsync(userId);
                return Ok(new { success = result, message = "Notification sent" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Send deadline reminder to user
        /// </summary>
        [HttpPost("users/{userId}/send-deadline-reminder")]
        [Authorize(Roles = "Admin,Department Admin")]
        public async Task<IActionResult> SendDeadlineReminder(string userId)
        {
            try
            {
                var result = await _complianceService.SendDeadlineReminderAsync(userId);
                return Ok(new { success = result, message = "Reminder sent" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===== HEALTH CHECK =====

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { success = true, message = "Compliance service is healthy", timestamp = DateTime.UtcNow });
        }
    }
}

using FourierIT_API.DTOs.Compliance;
using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _context;

        public ComplianceController(
            IComplianceService complianceService,
            UserManager<User> userManager,
            ILogger<ComplianceController> logger,
            AppDbContext context)
        {
            _complianceService = complianceService;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        // ===== COMPLIANCE CHECKS =====

        /// <summary>
        /// Check compliance for a specific user
        /// </summary>
        [HttpPost("users/{userId}/check")]
        [Authorize(Policy = "Compliance.Manage")]
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
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Check compliance for entire department
        /// </summary>
        [HttpPost("departments/{departmentId}/check")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> CheckDepartmentCompliance(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid();

                var status = await _complianceService.CheckDepartmentComplianceAsync(departmentId);
                return Ok(new { success = true, data = status });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Bulk check compliance for multiple users
        /// </summary>
        [HttpPost("bulk-check")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> BulkCheckCompliance([FromBody] BulkComplianceUpdateDto request)
        {
            try
            {
                var statuses = await _complianceService.BulkCheckComplianceAsync(request.UserIds);
                return Ok(new { success = true, checkedCount = statuses.Count, data = statuses });
            }
            catch
            {
                throw;
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
                if (currentUserId != userId && !User.IsInRole("Admin") && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId);
                var details = await _complianceService.GetUserComplianceDetailsAsync(userId);
                return Ok(new { success = true, data = details });
            }
            catch
            {
                throw;
            }
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        /// <summary>
        /// Reads a DB-layer set-based rollup. The joins and conditional document-status
        /// aggregation belong in SQL Server because the result spans request lines,
        /// target users/departments, and documents in one round trip.
        /// </summary>
        [HttpGet("institutions/{institutionId:int}/summary")]
        [Authorize(Policy = "Compliance.View")]
        public async Task<IActionResult> GetInstitutionComplianceSummary(int institutionId)
        {
            var result = await _context.InstitutionComplianceSummaries.FromSqlRaw(
                "EXEC dbo.GetInstitutionComplianceSummary @InstitutionId = {0}", institutionId)
                .ToListAsync();

            return Ok(new { success = true, data = result });
        }

        private async Task<bool> UserCanAccessDepartmentAsync(int departmentId)
        {
            if (User.IsInRole("Admin"))
                return true;

            if (!User.IsInRole("Department Admin") && !User.IsInRole("Stakeholder"))
                return false;

            var currentUser = await GetCurrentUserAsync();
            return currentUser != null && currentUser.DepartmentId == departmentId;
        }

        /// <summary>
        /// Get compliance details for all users in a department
        /// </summary>
        [HttpGet("departments/{departmentId}/users")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> GetDepartmentUserCompliance(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid();

                var details = await _complianceService.GetDepartmentUserComplianceAsync(departmentId);
                return Ok(new { success = true, count = details.Count, data = details });
            }
            catch
            {
                throw;
            }
        }

        // ===== DASHBOARD =====

        /// <summary>
        /// Get system-wide compliance dashboard
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> GetSystemDashboard()
        {
            try
            {
                var dashboard = await _complianceService.GetSystemDashboardAsync();
                return Ok(new { success = true, data = dashboard });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get department compliance dashboard
        /// </summary>
        [HttpGet("departments/{departmentId}/dashboard")]
        [Authorize(Policy = "Compliance.View")]
        public async Task<IActionResult> GetDepartmentDashboard(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid();

                var dashboard = await _complianceService.GetDepartmentDashboardAsync(departmentId);
                return Ok(new { success = true, data = dashboard });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get compliance statistics
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> GetStatistics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var stats = await _complianceService.GetComplianceStatisticsAsync(startDate, endDate);
                return Ok(new { success = true, data = stats });
            }
            catch
            {
                throw;
            }
        }

        // ===== DOCUMENTS =====

        /// <summary>
        /// Get compliance issues for a user's documents
        /// </summary>
        [HttpGet("users/{userId}/issues")]
        [Authorize(Policy = "Compliance.View")]
        public async Task<IActionResult> GetDocumentIssues(string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !User.IsInRole("Admin") && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId);
                var issues = await _complianceService.GetDocumentIssuesAsync(userId);
                return Ok(new { success = true, count = issues.Count, data = issues });
            }
            catch
            {
                throw;
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
                if (currentUserId != userId && !User.IsInRole("Admin") && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId);
                var missing = await _complianceService.IdentifyMissingDocumentsAsync(userId);
                return Ok(new { success = true, count = missing.Count, data = missing });
            }
            catch
            {
                throw;
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
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get overdue alerts
        /// </summary>
        [HttpGet("alerts/overdue")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> GetOverdueAlerts()
        {
            try
            {
                var alerts = await _complianceService.GetOverdueAlertsAsync();
                return Ok(new { success = true, count = alerts.Count, data = alerts });
            }
            catch
            {
                throw;
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
                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    return Unauthorized(new { error = "Unable to determine current user." });
                }

                var result = await _complianceService.AcknowledgeAlertAsync(alertId, currentUserId, request.AcknowledgmentNotes);

                return Ok(new { success = result, message = "Alert acknowledged" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Resolve an alert
        /// </summary>
        [HttpPut("alerts/{alertId}/resolve")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> ResolveAlert(int alertId, [FromBody] ResolveAlertDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    return Unauthorized(new { error = "Unable to determine current user." });
                }

                var result = await _complianceService.ResolveAlertAsync(alertId, currentUserId, request.ResolutionNotes);

                return Ok(new { success = result, message = "Alert resolved" });
            }
            catch
            {
                throw;
            }
        }

        // ===== APPROVALS =====

        /// <summary>
        /// Approve document compliance
        /// </summary>
        [HttpPost("documents/{checkId}/approve")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> ApproveDocumentCompliance(int checkId, [FromBody] ApproveDocumentComplianceDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    return Unauthorized(new { error = "Unable to determine current user." });
                }

                var result = await _complianceService.ApproveDocumentComplianceAsync(checkId, currentUserId, request.ApprovalNotes);

                return Ok(new { success = result, message = "Document compliance approved" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Reject document compliance
        /// </summary>
        [HttpPost("documents/{checkId}/reject")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> RejectDocumentCompliance(int checkId, [FromBody] ApproveDocumentComplianceDto request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    return Unauthorized(new { error = "Unable to determine current user." });
                }

                var result = await _complianceService.RejectDocumentComplianceAsync(checkId, currentUserId, request.ApprovalNotes);

                return Ok(new { success = result, message = "Document compliance rejected" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Bulk approve documents
        /// </summary>
        [HttpPost("documents/bulk-approve")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> BulkApproveDocuments([FromBody] List<int> checkIds)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    return Unauthorized(new { error = "Unable to determine current user." });
                }

                var count = await _complianceService.BulkApproveDocumentsAsync(checkIds, currentUserId);

                return Ok(new { success = true, approved = count });
            }
            catch
            {
                throw;
            }
        }

        // ===== DEADLINES =====

        /// <summary>
        /// Set compliance deadline for a user
        /// </summary>
        [HttpPost("users/{userId}/deadline")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> SetDeadline(string userId, [FromBody] SetComplianceDeadlineDto request)
        {
            try
            {
                var result = await _complianceService.SetComplianceDeadlineAsync(userId, request.Deadline, request.Reason);
                return Ok(new { success = result, message = "Deadline set successfully" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get users near their compliance deadline
        /// </summary>
        [HttpGet("users/deadline/near")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> GetUsersNearDeadline([FromQuery] int daysThreshold = 7)
        {
            try
            {
                var users = await _complianceService.GetUsersNearDeadlineAsync(daysThreshold);
                return Ok(new { success = true, count = users.Count, data = users });
            }
            catch
            {
                throw;
            }
        }

        // ===== ESCALATION =====

        /// <summary>
        /// Escalate a user's compliance for manual review
        /// </summary>
        [HttpPost("users/{userId}/escalate")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> EscalateCompliance(string userId, [FromBody] BulkComplianceUpdateDto request)
        {
            try
            {
                var result = await _complianceService.EscalateComplianceAsync(userId, request.Notes ?? "No reason provided");
                return Ok(new { success = result, message = "Compliance escalated for review" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Flag document for manual review
        /// </summary>
        [HttpPost("documents/{checkId}/flag-review")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> FlagForManualReview(int checkId, [FromBody] BulkComplianceUpdateDto request)
        {
            try
            {
                var result = await _complianceService.FlagForManualReviewAsync(checkId, request.Notes ?? "No reason provided");
                return Ok(new { success = result, message = "Document flagged for manual review" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get documents pending manual review
        /// </summary>
        [HttpGet("documents/pending-review")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> GetPendingManualReviews()
        {
            try
            {
                var documents = await _complianceService.GetPendingManualReviewsAsync();
                return Ok(new { success = true, count = documents.Count, data = documents });
            }
            catch
            {
                throw;
            }
        }

        // ===== HISTORY & AUDIT =====

        /// <summary>
        /// Get compliance history for a user
        /// </summary>
        [HttpGet("users/{userId}/history")]
        [Authorize]
        public async Task<IActionResult> GetComplianceHistory(string userId, [FromQuery] int limit = 50)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !User.IsInRole("Admin"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId);
                var status = await _complianceService.GetComplianceHistoryAsync(userId, limit);
                return Ok(new { success = true, count = status.Count, data = status });
            }
            catch
            {
                throw;
            }
        }

        [HttpGet("rules")]
        [Authorize]
        public async Task<IActionResult> GetComplianceRules()
        {
            try
            {
                var rules = await _complianceService.GetComplianceRulesAsync();
                return Ok(new { success = true, count = rules.Count, data = rules });
            }
            catch
            {
                throw;
            }
        }

        // ===== NOTIFICATIONS =====

        /// <summary>
        /// Send non-compliance notification to user
        /// </summary>
        [HttpPost("users/{userId}/send-notification")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> SendNonComplianceNotification(string userId)
        {
            try
            {
                var result = await _complianceService.SendNonComplianceNotificationAsync(userId);
                return Ok(new { success = result, message = "Notification sent" });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Send deadline reminder to user
        /// </summary>
        [HttpPost("users/{userId}/send-deadline-reminder")]
        [Authorize(Policy = "Compliance.Manage")]
        public async Task<IActionResult> SendDeadlineReminder(string userId)
        {
            try
            {
                var result = await _complianceService.SendDeadlineReminderAsync(userId);
                return Ok(new { success = result, message = "Reminder sent" });
            }
            catch
            {
                throw;
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

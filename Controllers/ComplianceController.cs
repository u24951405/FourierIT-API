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
                if (currentUserId != userId && !IsSuperAdmin() && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId, false);
                var details = await _complianceService.GetUserComplianceDetailsAsync(userId);
                return Ok(new { success = true, data = details });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// The Super Admin has no role: full access comes from the "superadmin" claim. ("Admin" is kept for older accounts.)
        /// </summary>
        private bool IsSuperAdmin() => User.HasClaim("superadmin", "true") || User.IsInRole("Admin");

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
            if (IsSuperAdmin())
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
        // Read-only summary: viewing needs Compliance.View (e.g. Stakeholders); changes still need Compliance.Manage.
        [Authorize(Policy = "Compliance.View")]
        public async Task<IActionResult> GetSystemDashboard()
        {
            // Department Admins see only their own department's figures (the department dashboard), never
            // organisation-wide totals. They hold Compliance.Manage, so this has to be a role check.
            var isOrganisationWideViewer = IsSuperAdmin();
            if (User.IsInRole("Department Admin") && !isOrganisationWideViewer)
                return Forbid();

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

        [HttpGet("dashboard-snapshot")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSystemDashboardSnapshot()
        {
            try
            {
                var snapshot = await _complianceService.GetSystemDashboardSnapshotAsync();
                return Ok(new { success = true, data = snapshot });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("departments/{departmentId}/dashboard-snapshot")]
        [Authorize(Roles = "Admin,Department Admin,Stakeholder")]
        public async Task<IActionResult> GetDepartmentDashboardSnapshot(int departmentId)
        {
            try
            {
                if (!await UserCanAccessDepartmentAsync(departmentId))
                    return Forbid();

                var snapshot = await _complianceService.GetDepartmentDashboardSnapshotAsync(departmentId);
                return Ok(new { success = true, data = snapshot });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("users/{userId}/dashboard-snapshot")]
        [Authorize]
        public async Task<IActionResult> GetUserDashboardSnapshot(string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !IsSuperAdmin() && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                var snapshot = await _complianceService.GetUserDashboardSnapshotAsync(userId);
                return Ok(new { success = true, data = snapshot });
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
                if (currentUserId != userId && !IsSuperAdmin() && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId, false);
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
                if (currentUserId != userId && !IsSuperAdmin() && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId, false);
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
                else if (userId != currentUserId && !IsSuperAdmin())
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

                // Only documents the automatic checks found nothing wrong with can be approved in bulk;
                // anything else needs a Compliance Officer to look at it on its own.
                // The same documents the review queue shows (GetPendingManualReviewsAsync), limited to the ones picked.
                var checks = (await _complianceService.GetPendingManualReviewsAsync())
                    .Where(c => checkIds.Contains(c.CheckId))
                    .ToList();
                var eligible = checks.Where(c => ClearlyValidConcerns(c).Count == 0).Select(c => c.CheckId).ToList();
                var skipped = checkIds.Except(eligible).ToList();

                var count = await _complianceService.BulkApproveDocumentsAsync(eligible, currentUserId);

                return Ok(new { success = true, approved = count, skipped });
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
        [Authorize(Policy = "Compliance.View")]
        public async Task<IActionResult> GetPendingManualReviews()
        {
            try
            {
                var documents = await _complianceService.GetPendingManualReviewsAsync();

                // Institution requests still waiting on these owners: a document one of them needs goes to the front.
                var ownerIds = documents.Select(check => check.Document?.UserId).Where(id => id != null).Distinct().ToList();
                var waitingRequests = await _context.InstitutionEnquiryRequests
                    .AsNoTracking()
                    .Where(r => r.TargetUserId != null && ownerIds.Contains(r.TargetUserId) && r.Status == "Pending")
                    .Select(r => new
                    {
                        r.TargetUserId,
                        r.SubmissionDeadline,
                        InstitutionName = r.Institution.InstitutionName,
                        TypeIds = r.RequestedDocumentTypes.Select(t => t.DocumentTypeId).ToList()
                    })
                    .ToListAsync();

                var now = DateTime.UtcNow;
                var data = documents.Select(check =>
                {
                    var neededFor = waitingRequests
                        .Where(r => r.TargetUserId == check.Document?.UserId && check.Document != null && r.TypeIds.Contains(check.Document.DocumentTypeId))
                        .OrderBy(r => r.SubmissionDeadline ?? DateTimeOffset.MaxValue)
                        .FirstOrDefault();
                    var concerns = ClearlyValidConcerns(check);

                    var user = check.Document?.User;
                    var profileName = user?.Profile == null
                        ? string.Empty
                        : string.Join(" ", new[] { user.Profile.FirstName, user.Profile.LastName }
                            .Where(name => !string.IsNullOrWhiteSpace(name)));

                    return new
                    {
                        checkId = check.CheckId,
                        documentId = check.DocumentId,
                        document = check.Document == null
                            ? null
                            : new
                            {
                                fileName = check.Document.FileName,
                                documentType = check.Document.DocumentType == null
                                    ? null
                                    : new { typeName = check.Document.DocumentType.TypeName }
                            },
                        fileName = check.Document?.FileName,
                        checkStatus = check.CheckStatus,
                        nonComplianceReason = check.NonComplianceReason,
                        manualReviewReason = check.ManualReviewReason,
                        qualityScore = check.QualityScore,
                        checkedAt = check.CheckedAt,
                        uploadedDate = check.Document?.UploadedDate,
                        ownerUserId = check.Document?.UserId,
                        ownerName = string.IsNullOrWhiteSpace(profileName)
                            ? user?.UserName
                            : profileName,
                        departmentName = user?.Department?.DepartmentName,
                        isExpiryValid = check.IsExpiryValid,
                        daysUntilExpiry = check.DaysUntilExpiry,
                        isCertified = check.IsCertified,
                        isRecent = check.IsRecent,
                        requiresManualReview = check.RequiresManualReview,
                        remediationAction = check.RemediationAction,
                        actionDueDate = check.ActionDueDate,
                        // How long it has been waiting for a decision.
                        waitingSince = check.CheckedAt,
                        waitingDays = (int)Math.Floor((now - check.CheckedAt).TotalDays),
                        neededBy = neededFor?.SubmissionDeadline,
                        neededByInstitution = neededFor?.InstitutionName,
                        isClearlyValid = concerns.Count == 0,
                        concerns
                    };
                }).ToList();

                return Ok(new { success = true, count = data.Count, data });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// What stops a document counting as clearly valid (nothing means it can be approved in bulk):
        /// the automatic checks found it expired, uncertified, unreadable or unsafe, or gave another reason.
        /// </summary>
        public static List<string> ClearlyValidConcerns(DocumentComplianceCheck check)
        {
            var concerns = new List<string>();
            if (!check.IsExpiryValid || check.DaysUntilExpiry is <= 0) concerns.Add("Expired or expiry date not valid");
            if (!check.IsCertified) concerns.Add("Not certified");
            if (!check.IsLegible || !check.IsHighQuality) concerns.Add("Hard to read");
            if (!check.IsVirusFree) concerns.Add("Failed the virus scan");
            if (!string.IsNullOrWhiteSpace(check.NonComplianceReason)) concerns.Add(check.NonComplianceReason!);
            return concerns;
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
                // The same people who can see someone's compliance details can see how it changed over time.
                if (currentUserId != userId && !IsSuperAdmin() && !User.IsInRole("Department Admin") && !User.IsInRole("Compliance Officer"))
                    return Forbid();

                await _complianceService.CheckUserComplianceAsync(userId, false);
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

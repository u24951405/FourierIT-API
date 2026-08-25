using FourierIT_API.Data;
using FourierIT_API.DTOs.Compliance;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FourierIT_API.Services
{
    /// <summary>
    /// Enterprise FICA Compliance Service
    /// Handles all compliance checking, tracking, and reporting
    /// </summary>
    public class ComplianceService : IComplianceService
    {
        private static readonly TimeSpan ComplianceCacheWindow = TimeSpan.FromMinutes(5);
        private static bool _defaultComplianceRulesSeeded;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ComplianceService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IAuditLogService _auditLogService;

        public ComplianceService(
            AppDbContext context,
            UserManager<User> userManager,
            ILogger<ComplianceService> logger,
            IAuditLogService auditLogService,
            INotificationService? notificationService = null)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _auditLogService = auditLogService;
            _notificationService = notificationService ?? new NullNotificationService();
        }

        // ===== MAIN COMPLIANCE CHECK =====

        public async Task<ComplianceStatus> CheckUserComplianceAsync(string userId, bool runDetailedCheck = true)
        {
            if (!runDetailedCheck)
            {
                var cachedStatus = await _context.ComplianceStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cs => cs.UserId == userId);

                if (cachedStatus != null
                    && cachedStatus.LastChecked >= DateTime.UtcNow.Subtract(ComplianceCacheWindow))
                {
                    return cachedStatus;
                }
            }

            await SeedDefaultComplianceRulesAsync();

            var user = await _userManager.FindByIdAsync(userId) 
                ?? throw new Exception($"User {userId} not found");

            var status = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.UserId == userId) 
                ?? new ComplianceStatus { UserId = userId };

            if (status.ComplianceStatusId == 0)
            {
                _context.ComplianceStatuses.Add(status);
                await _context.SaveChangesAsync();
            }

            var previousStatus = status.OverallStatus;
            var previousRiskLevel = status.RiskLevel;
            var previousComplianceScore = status.ComplianceScore;

            var requiredDocs = await GetRequiredDocumentsForUserAsync(user);
            var userDocs = await _context.Documents
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.CurrentStatus != "Deleted")
                .ToListAsync();

            // Initialize counters
            status.TotalRequiredDocuments = requiredDocs.Count(r => r.IsMandatory);
            status.UploadedDocuments = userDocs.Count;
            status.CompliantDocuments = 0;
            status.NonCompliantDocuments = 0;
            status.ExpiredDocuments = 0;
            status.MissingDocuments = 0;
            status.NotCertifiedDocuments = 0;
            status.PendingReviewDocuments = 0;

            var warningThresholdDays = await GetWarningThresholdDaysAsync();
            var requiredDocumentTypeIds = requiredDocs
                .Where(r => r.IsMandatory)
                .Select(r => r.DocumentTypeId)
                .Distinct()
                .ToList();
            var maxDocumentAgeByType = await GetMaxDocumentAgesAsync(requiredDocumentTypeIds);

            if (status.DocumentChecks.Any())
            {
                _context.DocumentComplianceChecks.RemoveRange(status.DocumentChecks);
                status.DocumentChecks.Clear();
            }

            // Check each required document
            foreach (var required in requiredDocs.Where(r => r.IsMandatory))
            {
                var doc = userDocs.FirstOrDefault(d => d.DocumentTypeId == required.DocumentTypeId);

                if (doc == null)
                {
                    status.MissingDocuments++;
                    status.NonCompliantDocuments++;
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var isExpiryValid = doc.ExpiryDate > now;
                var daysUntilExpiry = (int)(doc.ExpiryDate - now).TotalDays;

                DocumentComplianceCheck check;
                if (!isExpiryValid || !doc.IsManualOverrideActive)
                {
                    var maxMonthsOld = maxDocumentAgeByType.TryGetValue(doc.DocumentTypeId, out var configuredMaxMonths)
                        ? configuredMaxMonths
                        : 60;
                    check = await PerformDocumentCheckAsync(
                        doc.DocumentId,
                        status.ComplianceStatusId,
                        warningThresholdDays,
                        maxMonthsOld);
                }
                else
                {
                    check = new DocumentComplianceCheck
                    {
                        DocumentId = doc.DocumentId,
                        ComplianceStatusId = status.ComplianceStatusId,
                        CheckStatus = "Compliant",
                        IsExpiryValid = true,
                        ExpiryCheckDate = now.UtcDateTime,
                        DaysUntilExpiry = daysUntilExpiry,
                        IsCertified = true,
                        IsRecent = true,
                        IsEncrypted = doc.IsEncrypted,
                        IsVirusFree = true,
                        QualityScore = 100,
                        IsHighQuality = true,
                        IsLegible = true,
                        RequiresManualReview = false,
                        IsManuallyApproved = true,
                        ManuallyReviewedBy = doc.ManualOverrideBy,
                        ManualReviewDate = doc.ManualOverrideAt,
                        ManualReviewReason = doc.ManualOverrideReason,
                        RiskCategory = "Low",
                        IndividualRiskScore = 10,
                        CheckedAt = now.UtcDateTime,
                        DocumentTypeId = doc.DocumentTypeId
                    };

                    _context.DocumentComplianceChecks.Add(check);
                    await _context.SaveChangesAsync();
                }

                status.DocumentChecks.Add(check);

                if (check.CheckStatus == "Compliant")
                    status.CompliantDocuments++;
                else
                {
                    status.NonCompliantDocuments++;

                    if (!check.IsExpiryValid)
                        status.ExpiredDocuments++;
                    if (!check.IsCertified)
                        status.NotCertifiedDocuments++;
                }

                if (check.RequiresManualReview && !check.IsManuallyApproved)
                    status.PendingReviewDocuments++;
            }

            // Calculate scores
            status.CompliancePercentage = ComplianceEvaluationEngine.CalculateCompliancePercentage(
                status.TotalRequiredDocuments,
                status.CompliantDocuments,
                status.MissingDocuments,
                status.ExpiredDocuments,
                status.NonCompliantDocuments,
                status.PendingReviewDocuments);

            status.ComplianceScore = await CalculateComplianceScoreAsync(userId);
            status.OverallRiskScore = await AssessRiskLevelAsync(userId);

            // Tag the status with the user's department and compliance category.
            status.DepartmentId = user.DepartmentId;
            status.ComplianceCategory = DetermineComplianceCategory(user);

            // Determine overall status
            status.OverallStatus = DetermineOverallStatus(status);
            status.RiskLevel = DetermineRiskLevel(status);
            await PersistComplianceResultAsync(status);

            status.LastChecked = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;
            status.NextReviewDate = DateTime.UtcNow.AddDays(30);

            // Save
            _context.ComplianceStatuses.Update(status);
            await _context.SaveChangesAsync();

            await RecordComplianceHistoryAsync(status, previousStatus, previousRiskLevel, previousComplianceScore, "Automated compliance evaluation");
            await CreateAuditLogAsync(status.ComplianceStatusId, "ComplianceCheck",
                $"Compliance check completed: {status.OverallStatus}");
            _logger.LogInformation($"Compliance check for {userId}: {status.OverallStatus}");

            // Generate alerts if needed
            await GenerateAlertsAsync(status);

            return status;
        }

        public async Task<ComplianceStatus> CheckDepartmentComplianceAsync(int departmentId)
        {
            var dept = await _context.Departments
                .Include(d => d.DepartmentUsers)
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId)
                ?? throw new Exception($"Department {departmentId} not found");

            var deptStatus = new ComplianceStatus
            {
                DepartmentId = departmentId,
                OverallStatus = "Pending"
            };

            var userStatuses = new List<ComplianceStatus>();
            foreach (var user in dept.DepartmentUsers)
            {
                var status = await CheckUserComplianceAsync(user.Id);
                userStatuses.Add(status);
            }

            // Aggregate
            deptStatus.TotalRequiredDocuments = userStatuses.Sum(s => s.TotalRequiredDocuments);
            deptStatus.CompliantDocuments = userStatuses.Sum(s => s.CompliantDocuments);
            deptStatus.NonCompliantDocuments = userStatuses.Sum(s => s.NonCompliantDocuments);
            deptStatus.MissingDocuments = userStatuses.Sum(s => s.MissingDocuments);
            deptStatus.ExpiredDocuments = userStatuses.Sum(s => s.ExpiredDocuments);
            deptStatus.PendingReviewDocuments = userStatuses.Sum(s => s.PendingReviewDocuments);
            deptStatus.CompliancePercentage = ComplianceEvaluationEngine.CalculateCompliancePercentage(
                deptStatus.TotalRequiredDocuments,
                deptStatus.CompliantDocuments,
                deptStatus.MissingDocuments,
                deptStatus.ExpiredDocuments,
                deptStatus.NonCompliantDocuments,
                deptStatus.PendingReviewDocuments);

            deptStatus.OverallStatus = DetermineOverallStatus(deptStatus);

            return deptStatus;
        }

        public async Task<List<ComplianceStatus>> BulkCheckComplianceAsync(List<string> userIds)
        {
            var results = new List<ComplianceStatus>();
            foreach (var userId in userIds)
            {
                try
                {
                    var status = await CheckUserComplianceAsync(userId);
                    results.Add(status);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error checking compliance for {userId}: {ex.Message}");
                }
            }
            return results;
        }

        // ===== USER COMPLIANCE DETAILS =====

        public async Task<UserComplianceDto> GetUserComplianceDetailsAsync(string userId)
        {
            var status = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                    .ThenInclude(dc => dc.Document)
                        .ThenInclude(d => d.DocumentType)
                .Include(cs => cs.ComplianceAlerts)
                .FirstOrDefaultAsync(cs => cs.UserId == userId);

            if (status == null)
                return new UserComplianceDto();

            var user = await _userManager.FindByIdAsync(userId);
            var missingDocs = await IdentifyMissingDocumentsAsync(userId);

            var dto = new UserComplianceDto
            {
                UserId = userId,
                UserName = user?.UserName ?? "Unknown",
                Email = user?.Email ?? string.Empty,
                OverallStatus = status.OverallStatus,
                RiskLevel = status.RiskLevel,
                ComplianceCategory = status.ComplianceCategory,
                TotalRequired = status.TotalRequiredDocuments,
                Uploaded = status.UploadedDocuments,
                Compliant = status.CompliantDocuments,
                NonCompliant = status.NonCompliantDocuments,
                Expired = status.ExpiredDocuments,
                Missing = status.MissingDocuments,
                NotCertified = status.NotCertifiedDocuments,
                PendingReviewDocuments = status.PendingReviewDocuments,
                CompliancePercentage = status.CompliancePercentage,
                ComplianceScore = status.ComplianceScore,
                RiskScore = status.OverallRiskScore,
                WarningThresholdDays = await GetWarningThresholdDaysAsync(),
                LastChecked = status.LastChecked,
                ComplianceDeadline = status.ComplianceDeadline,
                RequiresEnhancedDueDiligence = status.RequiresEnhancedDueDiligence,
                IsPEP = status.IsPEP,
                HasSanctionFlag = status.HasSanctionFlag
            };

            dto.DocumentIssues = status.DocumentChecks
                .Where(dc => dc.CheckStatus != "Compliant")
                .Select(dc => new DocumentComplianceIssueDto
                {
                    DocumentId = dc.DocumentId,
                    DocumentName = dc.Document?.FileName ?? "Deleted Document",
                    DocumentType = dc.Document?.DocumentType?.TypeName ?? "Deleted",
                    Status = dc.CheckStatus,
                    Issue = dc.NonComplianceReason,
                    QualityScore = dc.QualityScore,
                    IsExpiryValid = dc.IsExpiryValid,
                    DaysUntilExpiry = dc.DaysUntilExpiry,
                    IsCertified = dc.IsCertified,
                    IsRecent = dc.IsRecent,
                    RemediationAction = dc.RemediationAction,
                    ActionDueDate = dc.ActionDueDate,
                    RequiresManualReview = dc.RequiresManualReview
                })
                .ToList();

            dto.MissingDocuments = missingDocs;

            dto.OpenAlerts = status.ComplianceAlerts
                .Where(a => !a.IsResolved)
                .Select(a => new ComplianceAlertDto
                {
                    AlertId = a.AlertId,
                    AlertType = a.AlertType,
                    Severity = a.Severity,
                    Message = a.AlertMessage,
                    CreatedAt = a.CreatedAt,
                    DueDate = a.DueDate,
                    RequiredAction = a.RequiredAction
                })
                .ToList();

            return dto;
        }

        public async Task<List<UserComplianceDto>> GetDepartmentUserComplianceAsync(int departmentId)
        {
            var users = await _context.Users
                .Where(u => u.DepartmentId == departmentId)
                .Select(u => u.Id)
                .ToListAsync();

            var results = new List<UserComplianceDto>();
            foreach (var userId in users)
            {
                var compliance = await GetUserComplianceDetailsAsync(userId);
                results.Add(compliance);
            }

            return results;
        }

        // ===== DASHBOARD =====

        public async Task<ComplianceDashboardDto> GetSystemDashboardAsync()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var statuses = await _context.ComplianceStatuses
                .AsNoTracking()
                .ToListAsync();

            var dashboard = new ComplianceDashboardDto
            {
                TotalUsers = totalUsers,
                CompliantUsers = statuses.Count(s => s.OverallStatus == "Compliant"),
                NonCompliantUsers = statuses.Count(s => s.OverallStatus == "Non-Compliant"),
                PartialCompliantUsers = statuses.Count(s => s.OverallStatus == "Partial"),
                ReviewRequiredUsers = statuses.Count(s => s.OverallStatus == "Review-Required"),
                PendingUsers = totalUsers - statuses.Count
            };

            dashboard.OverallCompliancePercentage = totalUsers > 0
                ? (dashboard.CompliantUsers * 100m) / totalUsers
                : 0;

            dashboard.AverageComplianceScore = statuses.Count > 0
                ? statuses.Average(s => s.ComplianceScore)
                : 0;

            dashboard.CriticalRiskUsers = statuses.Count(s => s.RiskLevel == "Critical");
            dashboard.HighRiskUsers = statuses.Count(s => s.RiskLevel == "High");
            dashboard.MediumRiskUsers = statuses.Count(s => s.RiskLevel == "Medium");
            dashboard.LowRiskUsers = statuses.Count(s => s.RiskLevel == "Low");

            // Department stats
            var departments = await _context.Departments
                .AsNoTracking()
                .ToListAsync();

            foreach (var dept in departments)
            {
                var deptStatuses = await _context.ComplianceStatuses
                    .AsNoTracking()
                    .Where(s => s.DepartmentId == dept.DepartmentId)
                    .ToListAsync();

                if (deptStatuses.Count > 0)
                {
                    var deptCompliants = deptStatuses.Count(s => s.OverallStatus == "Compliant");
                    var deptNonCompliant = deptStatuses
                        .Where(s => s.OverallStatus == "Non-Compliant")
                        .Select(s => new UserComplianceSummaryDto
                        {
                            UserId = s.UserId,
                            UserName = s.User?.UserName ?? "Unknown",
                            Status = s.OverallStatus,
                            RiskLevel = s.RiskLevel,
                            CompliancePercentage = s.CompliancePercentage,
                            OpenAlerts = s.ComplianceAlerts.Count(a => !a.IsResolved)
                        })
                        .ToList();

                    dashboard.Departments.Add(new DepartmentComplianceDto
                    {
                        DepartmentId = dept.DepartmentId,
                        DepartmentName = dept.DepartmentName,
                        TotalMembers = deptStatuses.Count,
                        CompliantMembers = deptCompliants,
                        NonCompliantMembers = deptNonCompliant.Count,
                        CompliancePercentage = deptCompliants * 100m / deptStatuses.Count,
                        AverageRiskScore = (decimal)deptStatuses.Average(s => s.OverallRiskScore),
                        OpenAlerts = deptStatuses.Sum(s => s.ComplianceAlerts.Count(a => !a.IsResolved)),
                        NonCompliantUsers = deptNonCompliant.Take(5).ToList()
                    });
                }
            }

            // Critical alerts
            dashboard.CriticalAlerts = await _context.ComplianceAlerts
                .AsNoTracking()
                .Where(a => !a.IsResolved && a.Severity == "Critical")
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new ComplianceAlertDto
                {
                    AlertId = a.AlertId,
                    AlertType = a.AlertType,
                    Severity = a.Severity,
                    Message = a.AlertMessage,
                    UserName = a.User != null ? a.User.UserName : null,
                    CreatedAt = a.CreatedAt,
                    RequiredAction = a.RequiredAction
                })
                .ToListAsync();

            dashboard.TotalOpenAlerts = await _context.ComplianceAlerts
                .AsNoTracking()
                .CountAsync(a => !a.IsResolved);

            dashboard.Statistics = await GetComplianceStatisticsAsync();
            dashboard.LastUpdated = DateTime.UtcNow;

            return dashboard;
        }

        public async Task<ComplianceDashboardDto> GetDepartmentDashboardAsync(int departmentId)
        {
            var dept = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId)
                ?? throw new Exception($"Department {departmentId} not found");

            var statuses = await _context.ComplianceStatuses
                .AsNoTracking()
                .Where(s => s.DepartmentId == departmentId)
                .ToListAsync();

            var dashboard = new ComplianceDashboardDto
            {
                TotalUsers = statuses.Count,
                CompliantUsers = statuses.Count(s => s.OverallStatus == "Compliant"),
                NonCompliantUsers = statuses.Count(s => s.OverallStatus == "Non-Compliant"),
                PartialCompliantUsers = statuses.Count(s => s.OverallStatus == "Partial"),
                ReviewRequiredUsers = statuses.Count(s => s.OverallStatus == "Review-Required"),
                CriticalRiskUsers = statuses.Count(s => s.RiskLevel == "Critical"),
                HighRiskUsers = statuses.Count(s => s.RiskLevel == "High"),
                MediumRiskUsers = statuses.Count(s => s.RiskLevel == "Medium"),
                LowRiskUsers = statuses.Count(s => s.RiskLevel == "Low")
            };

            dashboard.OverallCompliancePercentage = statuses.Count > 0
                ? (dashboard.CompliantUsers * 100m) / statuses.Count
                : 0;

            dashboard.AverageComplianceScore = statuses.Count > 0
                ? statuses.Average(s => s.ComplianceScore)
                : 0;

            dashboard.TotalUploaded = statuses.Sum(s => s.UploadedDocuments);
            dashboard.TotalVerified = statuses.Sum(s => s.CompliantDocuments);
            dashboard.TotalRejected = statuses.Sum(s => s.NonCompliantDocuments);
            dashboard.TotalMissing = statuses.Sum(s => s.MissingDocuments);
            dashboard.TotalPendingReview = statuses.Sum(s => s.PendingReviewDocuments);

            var warningThresholdDays = await GetWarningThresholdDaysAsync();
            dashboard.WarningThresholdDays = warningThresholdDays;

            var now = DateTimeOffset.UtcNow;
            var expiryCutoff = now.AddDays(warningThresholdDays);
            var departmentDocuments = await _context.Documents
                .AsNoTracking()
                .Where(d => d.User.DepartmentId == departmentId && d.CurrentStatus != "Deleted")
                .ToListAsync();

            dashboard.TotalExpiringSoon = departmentDocuments.Count(d =>
                d.ExpiryDate > now && d.ExpiryDate <= expiryCutoff);
            dashboard.TotalExpired = departmentDocuments.Count(d =>
                string.Equals(d.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                || d.ExpiryDate <= now);

            var statusIds = statuses.Select(s => s.ComplianceStatusId).ToList();

            dashboard.TotalOpenAlerts = await _context.ComplianceAlerts
                .AsNoTracking()
                .Where(a => !a.IsResolved && statusIds.Contains(a.ComplianceStatusId))
                .CountAsync();

            dashboard.CriticalAlerts = await _context.ComplianceAlerts
                .AsNoTracking()
                .Where(a => !a.IsResolved && a.Severity == "Critical" && statusIds.Contains(a.ComplianceStatusId))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ComplianceAlertDto
                {
                    AlertId = a.AlertId,
                    AlertType = a.AlertType,
                    Severity = a.Severity,
                    Message = a.AlertMessage,
                    UserName = a.User != null ? a.User.UserName : null,
                    CreatedAt = a.CreatedAt,
                    RequiredAction = a.RequiredAction
                })
                .ToListAsync();

            return dashboard;
        }

        public async Task<ComplianceDashboardSnapshotDto> GetSystemDashboardSnapshotAsync()
        {
            var statuses = await _context.ComplianceStatuses
                .AsNoTracking()
                .ToListAsync();

            return await BuildDashboardSnapshotAsync("system", null, statuses, null);
        }

        public async Task<ComplianceDashboardSnapshotDto> GetDepartmentDashboardSnapshotAsync(int departmentId)
        {
            var departmentExists = await _context.Departments
                .AsNoTracking()
                .AnyAsync(d => d.DepartmentId == departmentId);

            if (!departmentExists)
                throw new Exception($"Department {departmentId} not found");

            var statuses = await _context.ComplianceStatuses
                .AsNoTracking()
                .Where(s => s.DepartmentId == departmentId)
                .ToListAsync();

            return await BuildDashboardSnapshotAsync("department", departmentId.ToString(), statuses, departmentId);
        }

        public async Task<ComplianceDashboardSnapshotDto> GetUserDashboardSnapshotAsync(string userId)
        {
            var statuses = await _context.ComplianceStatuses
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .ToListAsync();

            return await BuildDashboardSnapshotAsync("owner", userId, statuses, null, userId);
        }

        private async Task<ComplianceDashboardSnapshotDto> BuildDashboardSnapshotAsync(
            string scope,
            string? scopeId,
            List<ComplianceStatus> statuses,
            int? departmentId,
            string? userId = null)
        {
            var statusIds = statuses.Select(s => s.ComplianceStatusId).ToList();
            var documentsQuery = _context.Documents
                .AsNoTracking()
                .Include(d => d.DocumentType)
                .Where(d => d.CurrentStatus != "Deleted");

            if (userId != null)
                documentsQuery = documentsQuery.Where(d => d.UserId == userId);
            else if (departmentId.HasValue)
                documentsQuery = documentsQuery.Where(d => d.User.DepartmentId == departmentId.Value);

            var documents = await documentsQuery.ToListAsync();
            var checks = statusIds.Count == 0
                ? new List<DocumentComplianceCheck>()
                : await _context.DocumentComplianceChecks
                    .AsNoTracking()
                    .Where(c => c.DocumentId.HasValue && statusIds.Contains(c.ComplianceStatusId))
                    .ToListAsync();

            var latestChecks = checks
                .Where(c => c.DocumentId.HasValue)
                .GroupBy(c => c.DocumentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(c => c.CheckedAt)
                        .ThenByDescending(c => c.CheckId)
                        .First());

            var overview = new DocumentsOverviewDto { Total = documents.Count };
            foreach (var document in documents)
            {
                latestChecks.TryGetValue(document.DocumentId, out var check);

                if (string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                    || check?.IsExpiryValid == false)
                {
                    overview.Expired++;
                }
                else if (string.Equals(check?.CheckStatus, "Non-Compliant", StringComparison.OrdinalIgnoreCase))
                {
                    overview.NonCompliant++;
                }
                else if (string.Equals(check?.CheckStatus, "Compliant", StringComparison.OrdinalIgnoreCase))
                {
                    overview.Compliant++;
                }
                else
                {
                    overview.Unchecked++;
                }
            }

            var warningThresholdDays = await GetWarningThresholdDaysAsync();
            var now = DateTimeOffset.UtcNow;
            var expiryCutoff = now.AddDays(warningThresholdDays);
            var expiringDocuments = documents
                .Where(document => string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                    || document.ExpiryDate <= expiryCutoff
                    || (latestChecks.TryGetValue(document.DocumentId, out var check) && !check.IsExpiryValid))
                .Select(document =>
                {
                    latestChecks.TryGetValue(document.DocumentId, out var check);
                    var isExpired = string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                        || document.ExpiryDate <= now
                        || check?.IsExpiryValid == false;

                    return new ExpiringDocumentDto
                    {
                        DocumentId = document.DocumentId,
                        DocumentName = document.FileName,
                        DocumentType = document.DocumentType?.TypeName ?? string.Empty,
                        ExpiryDate = document.ExpiryDate,
                        DaysRemaining = (int)Math.Floor((document.ExpiryDate - now).TotalDays),
                        Status = isExpired ? "Expired" : check?.CheckStatus ?? document.CurrentStatus
                    };
                })
                .OrderBy(document => document.DaysRemaining)
                .ToList();

            var riskCategory = statuses
                .OrderByDescending(status => status.RiskLevel == "Critical")
                .ThenByDescending(status => status.RiskLevel == "High")
                .ThenByDescending(status => status.RiskLevel == "Medium")
                .Select(status => status.RiskLevel)
                .FirstOrDefault() ?? "Unknown";

            return new ComplianceDashboardSnapshotDto
            {
                Scope = scope,
                ScopeId = scopeId,
                LastChecked = statuses.Count == 0 ? null : statuses.Max(status => (DateTime?)status.LastChecked),
                DocumentsOverview = overview,
                Risk = new RiskSummaryDto
                {
                    ComplianceScore = statuses.Count == 0 ? 0 : statuses.Average(status => status.ComplianceScore),
                    RiskCategory = riskCategory
                },
                ExpiringDocuments = expiringDocuments
            };
        }

        public async Task<ComplianceStatisticsDto> GetComplianceStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-1);
            endDate ??= DateTime.UtcNow;

            var checks = await _context.DocumentComplianceChecks
                .AsNoTracking()
                .Where(c => c.CheckedAt >= startDate && c.CheckedAt <= endDate)
                .ToListAsync();

            var alerts = await _context.ComplianceAlerts
                .AsNoTracking()
                .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .ToListAsync();

            return new ComplianceStatisticsDto
            {
                TotalChecksPerformed = checks.Count,
                TotalDocumentsProcessed = checks.DistinctBy(c => c.DocumentId).Count(),
                TotalIssuesIdentified = checks.Count(c => c.CheckStatus != "Compliant"),
                IssuesResolved = checks.Count(c => c.ActionCompleted),
                AverageComplianceScore = checks.Count > 0 ? (decimal)checks.Average(c => c.QualityScore) : 0,
                DocumentsExpiredThisMonth = checks.Count(c => !c.IsExpiryValid && c.CheckedAt.Month == DateTime.UtcNow.Month),
                MostCommonIssueType = checks
                    .Where(c => !string.IsNullOrEmpty(c.NonComplianceReason))
                    .GroupBy(c => c.NonComplianceReason)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "None"
            };
        }

        // ===== DOCUMENT CHECKS =====

        public async Task<DocumentComplianceCheck> PerformDocumentCheckAsync(
            int documentId,
            int complianceStatusId,
            int warningThresholdDays,
            int maxMonthsOld)
        {
            var document = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DocumentId == documentId)
                ?? throw new Exception($"Document {documentId} not found");

            var check = new DocumentComplianceCheck
            {
                DocumentId = documentId,
                ComplianceStatusId = complianceStatusId,
                CheckedAt = DateTime.UtcNow
            };

            var issues = new List<string>();

            // Check 1: Expiry validity
            check.IsExpiryValid = document.ExpiryDate > DateTimeOffset.UtcNow;
            check.ExpiryCheckDate = DateTime.UtcNow;
            check.DaysUntilExpiry = (int)(document.ExpiryDate - DateTimeOffset.UtcNow).TotalDays;

            if (!check.IsExpiryValid)
                issues.Add($"Expired ({document.ExpiryDate:yyyy-MM-dd})");
            else if (check.DaysUntilExpiry < warningThresholdDays)
                issues.Add($"Expiring soon ({check.DaysUntilExpiry} days)");

            // Check 2: Certification
            check.IsCertified = document.IsCertified;
            if (!check.IsCertified)
                issues.Add("Not certified copy");

            // Check 3: Recency
            var minDate = DateTime.UtcNow.AddMonths(-maxMonthsOld);
            check.IsRecent = document.UploadedDate >= minDate;
            check.DocumentAgeInMonths = (int)((DateTime.UtcNow - document.UploadedDate).TotalDays / 30.44);

            if (!check.IsRecent)
                issues.Add($"Older than {maxMonthsOld} months");

            // Check 4: Encryption
            check.IsEncrypted = document.IsEncrypted;
            check.IsVirusFree = true; // Assume virus free (would integrate with antivirus service in production)

            // Check 5: Quality scoring
            check.QualityScore = CalculateDocumentQualityScore(document);
            check.IsHighQuality = check.QualityScore >= 80;
            check.IsLegible = true; // Assume legible (would use OCR/AI in production)

            // Determine status
            if (issues.Count == 0 && check.QualityScore >= 80)
            {
                check.CheckStatus = "Compliant";
                check.RiskCategory = "Low";
                check.IndividualRiskScore = 10;
            }
            else if (issues.Any(i => i.Contains("Expired")) || !check.IsExpiryValid)
            {
                check.CheckStatus = "Non-Compliant";
                check.NonComplianceReason = string.Join(", ", issues);
                check.RiskCategory = "Critical";
                check.IndividualRiskScore = 90;
                check.RequiresManualReview = true;
            }
            else if (!check.IsCertified)
            {
                check.CheckStatus = "Non-Compliant";
                check.NonComplianceReason = "Not certified";
                check.RiskCategory = "High";
                check.IndividualRiskScore = 70;
                check.RequiresManualReview = true;
            }
            else if (check.QualityScore < 80)
            {
                check.CheckStatus = "Warning";
                check.NonComplianceReason = "Low quality document";
                check.RiskCategory = "Medium";
                check.IndividualRiskScore = 50;
                check.RequiresManualReview = true;
            }
            else
            {
                check.CheckStatus = "Compliant";
                check.RiskCategory = "Low";
                check.IndividualRiskScore = 20;
            }

            // Set remediation action
            if (check.CheckStatus != "Compliant")
            {
                check.RemediationAction = DetermineRemediationAction(issues);
                check.ActionDueDate = DateTime.UtcNow.AddDays(14);
            }

            _context.DocumentComplianceChecks.Add(check);
            await _context.SaveChangesAsync();

            return check;
        }

        public async Task<List<DocumentComplianceIssueDto>> GetDocumentIssuesAsync(string userId)
        {
            var status = await _context.ComplianceStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.UserId == userId);

            if (status == null)
                return new List<DocumentComplianceIssueDto>();

            return await _context.DocumentComplianceChecks
                .AsNoTracking()
                .Where(dc => dc.ComplianceStatusId == status.ComplianceStatusId && dc.CheckStatus != "Compliant")
                .Include(dc => dc.Document)
                    .ThenInclude(d => d.DocumentType)
                .Select(dc => new DocumentComplianceIssueDto
                {
                    DocumentId = dc.DocumentId,
                    DocumentName = dc.Document != null ? dc.Document.FileName : "Deleted Document",
                    DocumentType = dc.Document != null && dc.Document.DocumentType != null ? dc.Document.DocumentType.TypeName : "Deleted",
                    Status = dc.CheckStatus,
                    Issue = dc.NonComplianceReason,
                    QualityScore = dc.QualityScore,
                    IsExpiryValid = dc.IsExpiryValid,
                    DaysUntilExpiry = dc.DaysUntilExpiry,
                    IsCertified = dc.IsCertified,
                    IsRecent = dc.IsRecent,
                    RemediationAction = dc.RemediationAction,
                    ActionDueDate = dc.ActionDueDate,
                    RequiresManualReview = dc.RequiresManualReview
                })
                .ToListAsync();
        }

        public async Task<List<MissingDocumentDto>> IdentifyMissingDocumentsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return new List<MissingDocumentDto>();

            var required = await GetRequiredDocumentsForUserAsync(user);
            var uploaded = await _context.Documents
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.CurrentStatus != "Deleted")
                .Select(d => d.DocumentTypeId)
                .ToListAsync();

            var missing = new List<MissingDocumentDto>();
            foreach (var req in required.Where(r => r.IsMandatory))
            {
                if (!uploaded.Contains(req.DocumentTypeId))
                {
                    missing.Add(new MissingDocumentDto
                    {
                        DocumentTypeId = req.DocumentTypeId,
                        DocumentTypeName = req.DocumentType.TypeName,
                        IsMandatory = req.IsMandatory,
                        Reason = "Not uploaded"
                    });
                }
            }

            return missing;
        }

        // ===== ALERTS =====

        public async Task<List<ComplianceAlertDto>> GetOpenAlertsAsync(string? userId = null)
        {
            var query = _context.ComplianceAlerts.AsNoTracking().Where(a => !a.IsResolved);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.UserId == userId);

            return await query
                .OrderByDescending(a => a.Severity == "Critical")
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new ComplianceAlertDto
                {
                    AlertId = a.AlertId,
                    AlertType = a.AlertType,
                    Severity = a.Severity,
                    Message = a.AlertMessage,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.UserName : null,
                    DocumentId = a.DocumentId,
                    DocumentName = a.DocumentName,
                    CreatedAt = a.CreatedAt,
                    DueDate = a.DueDate,
                    IsResolved = a.IsResolved,
                    RequiredAction = a.RequiredAction,
                    Priority = a.Priority,
                    IsEscalated = a.IsEscalated
                })
                .ToListAsync();
        }

        public async Task<ComplianceAlertDto> CreateAlertAsync(ComplianceAlert alert)
        {
            _context.ComplianceAlerts.Add(alert);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Alert created: {alert.AlertType} for {alert.UserId}");

            return new ComplianceAlertDto
            {
                AlertId = alert.AlertId,
                AlertType = alert.AlertType,
                Severity = alert.Severity,
                Message = alert.AlertMessage,
                CreatedAt = alert.CreatedAt,
                Priority = alert.Priority
            };
        }

        public async Task<bool> AcknowledgeAlertAsync(int alertId, string acknowledgedBy, string notes)
        {
            var alert = await _context.ComplianceAlerts.FindAsync(alertId)
                ?? throw new Exception($"Alert {alertId} not found");

            alert.IsAcknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = acknowledgedBy;

            _context.ComplianceAlerts.Update(alert);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ResolveAlertAsync(int alertId, string resolvedBy, string resolutionNotes)
        {
            var alert = await _context.ComplianceAlerts.FindAsync(alertId)
                ?? throw new Exception($"Alert {alertId} not found");

            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedBy = resolvedBy;
            alert.ResolutionNotes = resolutionNotes;

            _context.ComplianceAlerts.Update(alert);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<ComplianceAlertDto>> GetOverdueAlertsAsync()
        {
            return await _context.ComplianceAlerts
                .AsNoTracking()
                .Where(a => !a.IsResolved && a.DueDate < DateTime.UtcNow)
                .OrderByDescending(a => a.DueDate)
                .Select(a => new ComplianceAlertDto
                {
                    AlertId = a.AlertId,
                    AlertType = a.AlertType,
                    Severity = a.Severity,
                    Message = a.AlertMessage,
                    UserName = a.User != null ? a.User.UserName : null,
                    DueDate = a.DueDate,
                    RequiredAction = a.RequiredAction
                })
                .ToListAsync();
        }

        // ===== APPROVALS =====

        public async Task<bool> ApproveDocumentComplianceAsync(int checkId, string approvedBy, string notes)
        {
            var check = await _context.DocumentComplianceChecks.FindAsync(checkId)
                ?? throw new Exception($"Check {checkId} not found");

            var approver = await _userManager.FindByIdAsync(approvedBy)
                ?? throw new Exception($"Approver {approvedBy} not found");

            var status = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == check.ComplianceStatusId);

            if (status == null)
                throw new Exception($"Compliance status {check.ComplianceStatusId} not found");

            var previousStatus = status.OverallStatus;
            var previousRiskLevel = status.RiskLevel;
            var previousComplianceScore = status.ComplianceScore;
            var approvalTime = DateTime.UtcNow;

            var document = check.DocumentId.HasValue
                ? await _context.Documents.FindAsync(check.DocumentId.Value)
                : null;

            if (document == null)
                throw new Exception($"Document for check {checkId} not found");

            check.CheckStatus = "Compliant";
            check.IsManuallyApproved = true;
            check.ManuallyReviewedBy = approvedBy;
            check.ManualReviewDate = approvalTime;

            document.IsManualOverrideActive = true;
            document.ManualOverrideBy = approvedBy;
            document.ManualOverrideAt = approvalTime;
            document.ManualOverrideReason = notes;

            _context.DocumentComplianceChecks.Update(check);
            _context.Documents.Update(document);
            await _context.SaveChangesAsync();

            await UpdateComplianceStatusAsync(status.ComplianceStatusId);

            var updatedStatus = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == status.ComplianceStatusId)
                ?? status;

            await PersistComplianceResultAsync(updatedStatus);
            await RecordComplianceHistoryAsync(updatedStatus, previousStatus, previousRiskLevel, previousComplianceScore, "Document manually approved");
            await CreateAuditLogAsync(updatedStatus.ComplianceStatusId, "DocumentApproved", $"Document compliance approved for check {checkId}");
            await GenerateAlertsAsync(updatedStatus);

            _logger.LogInformation($"Document check {checkId} approved by {approvedBy}");

            return true;
        }

        public async Task<bool> RejectDocumentComplianceAsync(int checkId, string rejectedBy, string reason)
        {
            var check = await _context.DocumentComplianceChecks.FindAsync(checkId)
                ?? throw new Exception($"Check {checkId} not found");

            var status = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == check.ComplianceStatusId);

            if (status == null)
                throw new Exception($"Compliance status {check.ComplianceStatusId} not found");

            var previousStatus = status.OverallStatus;
            var previousRiskLevel = status.RiskLevel;
            var previousComplianceScore = status.ComplianceScore;

            var document = check.DocumentId.HasValue
                ? await _context.Documents.FindAsync(check.DocumentId.Value)
                : null;

            if (document == null)
                throw new Exception($"Document for check {checkId} not found");

            check.CheckStatus = "Non-Compliant";
            check.IsManuallyApproved = false;
            check.NonComplianceReason = reason;
            check.ManuallyReviewedBy = rejectedBy;
            check.ManualReviewDate = DateTime.UtcNow;

            document.IsManualOverrideActive = false;
            document.ManualOverrideBy = null;
            document.ManualOverrideAt = null;
            document.ManualOverrideReason = null;

            _context.DocumentComplianceChecks.Update(check);
            _context.Documents.Update(document);
            await _context.SaveChangesAsync();

            await UpdateComplianceStatusAsync(status.ComplianceStatusId);

            var updatedStatus = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == status.ComplianceStatusId)
                ?? status;

            await PersistComplianceResultAsync(updatedStatus);
            await RecordComplianceHistoryAsync(updatedStatus, previousStatus, previousRiskLevel, previousComplianceScore, "Document manually rejected");
            await CreateAuditLogAsync(updatedStatus.ComplianceStatusId, "DocumentRejected", $"Document compliance rejected for check {checkId}");
            await GenerateAlertsAsync(updatedStatus);

            _logger.LogInformation($"Document check {checkId} rejected by {rejectedBy}");

            return true;
        }

        // ===== DEADLINES =====

        public async Task<bool> SetComplianceDeadlineAsync(string userId, DateTime deadline, string reason)
        {
            var status = await _context.ComplianceStatuses
                .FirstOrDefaultAsync(cs => cs.UserId == userId)
                ?? throw new Exception($"Compliance status for user {userId} not found");

            status.ComplianceDeadline = deadline;
            status.LastChecked = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;

            _context.ComplianceStatuses.Update(status);
            await _context.SaveChangesAsync();

            // Create alert for user
            var alert = new ComplianceAlert
            {
                ComplianceStatusId = status.ComplianceStatusId,
                AlertType = "Deadline",
                Severity = "High",
                AlertMessage = $"Compliance deadline set: {deadline:yyyy-MM-dd}",
                UserId = userId,
                DueDate = deadline,
                RequiredAction = "Complete compliance requirements by deadline"
            };

            await CreateAlertAsync(alert);

            return true;
        }

        public async Task<List<UserComplianceSummaryDto>> GetUsersNearDeadlineAsync(int daysThreshold = 7)
        {
            var threshold = DateTime.UtcNow.AddDays(daysThreshold);

            return await _context.ComplianceStatuses
                .AsNoTracking()
                .Where(s => s.ComplianceDeadline != null && 
                            s.ComplianceDeadline <= threshold && 
                            s.ComplianceDeadline > DateTime.UtcNow &&
                            s.OverallStatus != "Compliant")
                .Include(s => s.User)
                .Select(s => new UserComplianceSummaryDto
                {
                    UserId = s.UserId,
                    UserName = s.User.UserName,
                    Status = s.OverallStatus,
                    RiskLevel = s.RiskLevel,
                    CompliancePercentage = s.CompliancePercentage,
                    OpenAlerts = s.ComplianceAlerts.Count(a => !a.IsResolved)
                })
                .ToListAsync();
        }

        // ===== ESCALATION =====

        public async Task<bool> EscalateComplianceAsync(string userId, string reason)
        {
            var status = await _context.ComplianceStatuses
                .FirstOrDefaultAsync(cs => cs.UserId == userId)
                ?? throw new Exception($"Compliance status for user {userId} not found");

            status.OverallStatus = "Review-Required";
            status.LastChecked = DateTime.UtcNow;
            _context.ComplianceStatuses.Update(status);

            var alert = new ComplianceAlert
            {
                ComplianceStatusId = status.ComplianceStatusId,
                AlertType = "Escalation",
                Severity = "Critical",
                AlertMessage = $"Compliance escalated: {reason}",
                UserId = userId,
                IsEscalated = true,
                ActionRequiredFromAdmin = true
            };

            await CreateAlertAsync(alert);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> FlagForManualReviewAsync(int checkId, string reason)
        {
            var check = await _context.DocumentComplianceChecks.FindAsync(checkId)
                ?? throw new Exception($"Check {checkId} not found");

            check.RequiresManualReview = true;
            check.ManualReviewReason = reason;

            var status = await _context.ComplianceStatuses
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == check.ComplianceStatusId);

            if (status != null)
                status.LastChecked = DateTime.UtcNow;

            _context.DocumentComplianceChecks.Update(check);
            if (status != null)
                _context.ComplianceStatuses.Update(status);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<DocumentComplianceCheck>> GetPendingManualReviewsAsync()
        {
            return await _context.DocumentComplianceChecks
                .AsNoTracking()
                .Where(c => c.RequiresManualReview && !c.IsManuallyApproved)
                .Include(c => c.Document)
                    .ThenInclude(d => d!.User)
                        .ThenInclude(u => u.Profile)
                .Include(c => c.Document)
                    .ThenInclude(d => d!.User)
                        .ThenInclude(u => u.Department)
                .Include(c => c.Document)
                    .ThenInclude(d => d!.DocumentType)
                .Include(c => c.ComplianceStatus)
                    .ThenInclude(cs => cs.User)
                .ToListAsync();
        }

        // ===== REPORTING =====

        public async Task<byte[]> GenerateComplianceReportAsync(DateTime startDate, DateTime endDate, int? departmentId = null)
        {
            // This would generate a PDF report - simplified for now
            // In production, use iText, PdfSharp, or similar library
            throw new NotImplementedException("PDF generation would use iText or similar library");
        }

        public async Task<byte[]> GenerateAuditReportAsync(int complianceStatusId)
        {
            throw new NotImplementedException("PDF generation would use iText or similar library");
        }

        // ===== HISTORY & AUDIT =====

        public async Task<List<ComplianceHistory>> GetComplianceHistoryAsync(int complianceStatusId, int limit = 50)
        {
            return await _context.ComplianceHistories
                .AsNoTracking()
                .Where(h => h.ComplianceStatusId == complianceStatusId)
                .OrderByDescending(h => h.ChangedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<ComplianceHistoryItemDto>> GetComplianceHistoryAsync(string userId, int limit = 50)
        {
            var status = await _context.ComplianceStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.UserId == userId);

            if (status == null)
                return new List<ComplianceHistoryItemDto>();

            return await _context.ComplianceHistories
                .AsNoTracking()
                .Where(h => h.ComplianceStatusId == status.ComplianceStatusId)
                .OrderByDescending(h => h.ChangedAt)
                .Take(limit)
                .Select(h => new ComplianceHistoryItemDto
                {
                    Status = h.NewStatus,
                    CompliancePercentage = h.NewComplianceScore ?? 0,
                    ChangeReason = h.ChangeReason,
                    ChangedAt = h.ChangedAt
                })
                .ToListAsync();
        }

        public async Task<List<ComplianceAuditLog>> GetAuditLogsAsync(int complianceStatusId, int limit = 100)
        {
            return await _context.ComplianceAuditLogs
                .AsNoTracking()
                .Where(al => al.ComplianceStatusId == complianceStatusId)
                .OrderByDescending(al => al.PerformedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<ComplianceRuleDto>> GetComplianceRulesAsync()
        {
            return await _context.ComplianceRules
                .AsNoTracking()
                .Where(r => r.IsActive && r.AppliesTo != "DocumentType")
                .OrderBy(r => r.RuleName)
                .Select(r => new ComplianceRuleDto
                {
                    ComplianceRuleId = r.ComplianceRuleId,
                    RuleName = r.RuleName,
                    AppliesTo = r.AppliesTo,
                    IsMandatory = r.IsMandatory,
                    ValidationRules = r.ValidationRules,
                    IsActive = r.IsActive,
                    Description = r.Description
                })
                .ToListAsync();
        }

        // ===== BULK OPERATIONS =====

        public async Task<int> BulkApproveDocumentsAsync(List<int> checkIds, string approvedBy)
        {
            int approved = 0;
            foreach (var checkId in checkIds)
            {
                try
                {
                    await ApproveDocumentComplianceAsync(checkId, approvedBy, "Bulk approved");
                    approved++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error approving check {checkId}: {ex.Message}");
                }
            }
            return approved;
        }

        public async Task<int> BulkRejectDocumentsAsync(List<int> checkIds, string rejectedBy, string reason)
        {
            int rejected = 0;
            foreach (var checkId in checkIds)
            {
                try
                {
                    await RejectDocumentComplianceAsync(checkId, rejectedBy, reason);
                    rejected++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error rejecting check {checkId}: {ex.Message}");
                }
            }
            return rejected;
        }

        // ===== UTILITIES =====

        public async Task<int> CalculateComplianceScoreAsync(string userId)
        {
            var status = await _context.ComplianceStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.UserId == userId);

            if (status == null || status.TotalRequiredDocuments == 0)
                return 0;

            int score = (status.CompliantDocuments * 100) / status.TotalRequiredDocuments;
            return Math.Max(0, Math.Min(100, score));
        }

        public async Task<int> AssessRiskLevelAsync(string userId)
        {
            var status = await _context.ComplianceStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.UserId == userId);

            if (status == null)
                return 50; // Default medium risk

            int riskScore = 0;

            if (status.ExpiredDocuments > 0)
                riskScore += 30;

            if (status.NotCertifiedDocuments > 0)
                riskScore += 20;

            if (status.MissingDocuments > 0)
                riskScore += 25;

            if (status.IsPEP)
                riskScore += 25;

            if (status.RequiresEnhancedDueDiligence)
                riskScore += 15;

            return Math.Max(0, Math.Min(100, riskScore));
        }

        public async Task<bool> UpdateComplianceStatusAsync(int statusId)
        {
            var status = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == statusId)
                ?? throw new Exception($"Status {statusId} not found");

            status.CompliantDocuments = status.DocumentChecks.Count(c => c.CheckStatus == "Compliant");
            status.PendingReviewDocuments = status.DocumentChecks.Count(c => c.CheckStatus == "Pending");
            status.NonCompliantDocuments = status.DocumentChecks.Count(c => c.CheckStatus != "Compliant");
            status.ExpiredDocuments = status.DocumentChecks.Count(c => !c.IsExpiryValid);
            status.NotCertifiedDocuments = status.DocumentChecks.Count(c => !c.IsCertified);

            var user = await _userManager.FindByIdAsync(status.UserId);
            if (user != null)
            {
                var requiredDocs = await GetRequiredDocumentsForUserAsync(user);
                status.TotalRequiredDocuments = requiredDocs.Count(r => r.IsMandatory);
            }
            else
            {
                status.TotalRequiredDocuments = 0;
            }

            var missingDocs = await IdentifyMissingDocumentsAsync(status.UserId);
            status.MissingDocuments = missingDocs.Count;
            status.UploadedDocuments = await _context.Documents
                .CountAsync(d => d.UserId == status.UserId && d.CurrentStatus != "Deleted");

            status.CompliancePercentage = ComplianceEvaluationEngine.CalculateCompliancePercentage(
                status.TotalRequiredDocuments,
                status.CompliantDocuments,
                status.MissingDocuments,
                status.ExpiredDocuments,
                status.NonCompliantDocuments,
                status.PendingReviewDocuments);

            status.ComplianceScore = await CalculateComplianceScoreAsync(status.UserId);
            status.OverallRiskScore = await AssessRiskLevelAsync(status.UserId);
            status.OverallStatus = DetermineOverallStatus(status);
            status.RiskLevel = DetermineRiskLevel(status);
            status.LastChecked = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;

            _context.ComplianceStatuses.Update(status);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ProcessExpiredDocumentComplianceAsync()
        {
            var expiredDocuments = await _context.Documents
                .Where(d => d.CurrentStatus != "Deleted" && d.ExpiryDate <= DateTimeOffset.UtcNow)
                .ToListAsync();

            foreach (var document in expiredDocuments)
            {
                if (!string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase))
                {
                    document.CurrentStatus = "Expired";

                    await _auditLogService.CreateAuditLogAsync(new AuditLog
                    {
                        UserId = document.UserId,
                        ActionCode = "DOCUMENT_EXPIRED",
                        TimeStamp = DateTimeOffset.UtcNow,
                        Description = $"Document marked Expired by automated compliance sweep. DocumentId={document.DocumentId}.",
                        TableAffected = "Documents",
                        RecordID = document.DocumentId
                    });
                }
            }

            if (expiredDocuments.Any())
            {
                await _context.SaveChangesAsync();
            }

            var expiredOwnerIds = await _context.Documents
                .AsNoTracking()
                .Where(d => d.CurrentStatus != "Deleted" && d.ExpiryDate <= DateTimeOffset.UtcNow)
                .Select(d => d.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var userId in expiredOwnerIds)
            {
                try
                {
                    await CheckUserComplianceAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to recalculate compliance for expired documents for user {UserId}", userId);
                }
            }

            return true;
        }

        // ===== CONFIGURATION =====

        public async Task<bool> ConfigureComplianceRulesAsync(string complianceCategory, Dictionary<string, object> rules)
        {
            // This would store compliance rules in database or cache
            // Implementation depends on specific requirements
            throw new NotImplementedException("Rule configuration storage not yet implemented");
        }

        public async Task<Dictionary<string, object>> GetComplianceRulesAsync(string complianceCategory)
        {
            throw new NotImplementedException("Rule retrieval not yet implemented");
        }

        // ===== NOTIFICATIONS =====

        public async Task<bool> SendNonComplianceNotificationAsync(string userId)
        {
            var compliance = await GetUserComplianceDetailsAsync(userId);

            if (compliance.OverallStatus != "Non-Compliant")
                return false;

            await _notificationService.SendNonComplianceAlertAsync(userId, compliance);
            return true;
        }

        public async Task<bool> SendDeadlineReminderAsync(string userId)
        {
            var status = await _context.ComplianceStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.UserId == userId);

            if (status?.ComplianceDeadline != null)
            {
                await _notificationService.SendDeadlineReminderAsync(userId, status.ComplianceDeadline.Value);
                return true;
            }

            return false;
        }

        // ===== PRIVATE HELPER METHODS =====

        private string DetermineOverallStatus(ComplianceStatus status)
        {
            return ComplianceEvaluationEngine.ResolveOverallStatus(
                status.TotalRequiredDocuments,
                status.MissingDocuments,
                status.ExpiredDocuments,
                status.NonCompliantDocuments,
                status.PendingReviewDocuments,
                status.CompliantDocuments);
        }

        private string DetermineRiskLevel(ComplianceStatus status)
        {
            int riskScore = 0;

            if (status.ExpiredDocuments > 0)
                riskScore += 30;
            if (status.NotCertifiedDocuments > 0)
                riskScore += 20;
            if (status.MissingDocuments > 0)
                riskScore += 25;
            if (status.IsPEP)
                riskScore += 25;
            if (status.RequiresEnhancedDueDiligence)
                riskScore += 15;

            if (riskScore >= 75)
                return "Critical";
            if (riskScore >= 50)
                return "High";
            if (riskScore >= 25)
                return "Medium";

            return "Low";
        }

        private int CalculateDocumentQualityScore(Document document)
        {
            int score = 100;

            if (document.ExpiryDate <= DateTimeOffset.UtcNow)
                score -= 50;

            if (!document.IsCertified)
                score -= 30;

            if (document.FileSizeBytes == 0)
                score -= 20;

            if (!document.IsEncrypted)
                score -= 10;

            return Math.Max(0, score);
        }

        private async Task<Dictionary<int, int>> GetMaxDocumentAgesAsync(List<int> documentTypeIds)
        {
            if (documentTypeIds.Count == 0)
                return new Dictionary<int, int>();

            var rules = await _context.ComplianceRules
                .AsNoTracking()
                .Where(r => r.AppliesTo == "DocumentType"
                    && r.IsActive
                    && r.ExpiryPeriodDays.HasValue
                    && (!r.RequiredDocumentTypeId.HasValue || documentTypeIds.Contains(r.RequiredDocumentTypeId.Value)))
                .OrderByDescending(r => r.ComplianceRuleId)
                .ToListAsync();

            var defaultRule = rules.FirstOrDefault(r => !r.RequiredDocumentTypeId.HasValue);
            var maxDocumentAges = new Dictionary<int, int>();

            foreach (var documentTypeId in documentTypeIds)
            {
                var typeSpecificRule = rules.FirstOrDefault(r => r.RequiredDocumentTypeId == documentTypeId);
                var expiryPeriodDays = typeSpecificRule?.ExpiryPeriodDays ?? defaultRule?.ExpiryPeriodDays;
                maxDocumentAges[documentTypeId] = expiryPeriodDays is int days ? days / 30 : 60;
            }

            return maxDocumentAges;
        }

        private async Task<int> GetWarningThresholdDaysAsync()
        {
            var rule = await _context.ComplianceRules
                .AsNoTracking()
                .Where(r => r.AppliesTo == "DocumentType"
                    && r.IsActive
                    && !r.RequiredDocumentTypeId.HasValue
                    && r.WarningThresholdDays.HasValue)
                .OrderByDescending(r => r.ComplianceRuleId)
                .FirstOrDefaultAsync();

            return rule?.WarningThresholdDays ?? 30;
        }

        private string DetermineRemediationAction(List<string> issues)
        {
            if (issues.Any(i => i.Contains("Expired")))
                return "Renew document and reupload";
            if (issues.Any(i => i.Contains("Not certified")))
                return "Get certified copy and reupload";
            if (issues.Any(i => i.Contains("Older than")))
                return "Reupload with recent copy";
            return "Reupload document";
        }

        private async Task<List<RequiredDocument>> GetRequiredDocumentsForUserAsync(User user)
        {
            var entityTypeId = user.EntityTypeId ?? 1;

            return await _context.RequiredDocuments
                .AsNoTracking()
                .Include(rd => rd.DocumentType)
                .Where(rd => rd.EntityTypeId == entityTypeId)
                .ToListAsync();
        }

        private string DetermineComplianceCategory(User user)
        {
            return user.EntityTypeId switch
            {
                3 => "Business",
                4 => "Trust",
                5 => "Partnership",
                6 => "Legal Entity",
                _ => "Individual"
            };
        }

        private async Task PersistComplianceResultAsync(ComplianceStatus status)
        {
            var latestResult = await _context.ComplianceResults
                .AsNoTracking()
                .Where(cr => cr.ComplianceStatusId == status.ComplianceStatusId)
                .OrderByDescending(cr => cr.CreatedAt)
                .FirstOrDefaultAsync();

            var result = new ComplianceResult
            {
                ComplianceStatusId = status.ComplianceStatusId,
                Scope = status.DepartmentId.HasValue ? "Department" : "User",
                TotalRequired = status.TotalRequiredDocuments,
                MissingDocuments = status.MissingDocuments,
                ExpiredDocuments = status.ExpiredDocuments,
                RejectedDocuments = status.NonCompliantDocuments,
                PendingVerification = status.PendingReviewDocuments,
                ValidDocuments = status.CompliantDocuments,
                DuplicateDocuments = 0,
                CompliancePercentage = status.CompliancePercentage,
                OverallStatus = status.OverallStatus,
                Summary = $"{status.CompliantDocuments} valid, {status.MissingDocuments} missing, {status.ExpiredDocuments} expired, {status.PendingReviewDocuments} pending",
                CreatedAt = DateTime.UtcNow
            };

            if (latestResult != null &&
                latestResult.OverallStatus == result.OverallStatus &&
                latestResult.CompliancePercentage == result.CompliancePercentage &&
                latestResult.MissingDocuments == result.MissingDocuments &&
                latestResult.ExpiredDocuments == result.ExpiredDocuments &&
                latestResult.RejectedDocuments == result.RejectedDocuments &&
                latestResult.ValidDocuments == result.ValidDocuments)
            {
                return;
            }

            _context.ComplianceResults.Add(result);
            await _context.SaveChangesAsync();
        }

        private async Task RecordComplianceHistoryAsync(ComplianceStatus status, string previousStatus, string? previousRiskLevel, decimal? previousComplianceScore, string reason)
        {
            if (previousStatus == status.OverallStatus && previousRiskLevel == status.RiskLevel && previousComplianceScore == status.ComplianceScore)
                return;

            var historyEntry = new ComplianceHistory
            {
                ComplianceStatusId = status.ComplianceStatusId,
                PreviousStatus = previousStatus,
                NewStatus = status.OverallStatus,
                PreviousRiskLevel = previousRiskLevel,
                NewRiskLevel = status.RiskLevel,
                PreviousComplianceScore = previousComplianceScore is null ? null : (int?)Math.Round(previousComplianceScore.Value),
                NewComplianceScore = (int?)Math.Round(status.ComplianceScore),
                ChangeReason = reason,
                Details = $"Compliance recalculated with {status.CompliantDocuments} compliant documents and {status.MissingDocuments} missing documents.",
                ChangedAt = DateTime.UtcNow,
                ChangeSource = "System"
            };

            _context.ComplianceHistories.Add(historyEntry);
            await _context.SaveChangesAsync();
        }

        private async Task SeedDefaultComplianceRulesAsync()
        {
            if (_defaultComplianceRulesSeeded)
                return;

            var ownerRuleName = "Document Owner Minimum KYC";
            var departmentRuleName = "Department Compliance Checklist";
            var defaultDocumentRuleName = "Default Document Max Age";
            var warningThresholdRuleName = "Global Expiry Warning Threshold";
            var addressDocumentRuleNameBase = "Address Document Max Age";

            var existingRules = await _context.ComplianceRules
                .AsNoTracking()
                .Where(r => r.RuleName == ownerRuleName || r.RuleName == departmentRuleName
                    || r.RuleName == defaultDocumentRuleName || r.RuleName == warningThresholdRuleName
                    || r.AppliesTo == "DocumentType" && (r.RuleName.StartsWith(addressDocumentRuleNameBase)))
                .ToListAsync();

            var ownerRule = existingRules.FirstOrDefault(r => r.RuleName == ownerRuleName);
            var departmentRule = existingRules.FirstOrDefault(r => r.RuleName == departmentRuleName);
            var defaultDocumentRule = existingRules.FirstOrDefault(r => r.RuleName == defaultDocumentRuleName);
            var warningThresholdRule = existingRules.FirstOrDefault(r => r.RuleName == warningThresholdRuleName);

            if (ownerRule == null)
            {
                ownerRule = new ComplianceRule
                {
                    RuleName = ownerRuleName,
                    AppliesTo = "DocumentOwner",
                    IsMandatory = true,
                    ValidationRules = "Require identity, address, tax number and bank confirmation",
                    Description = "Required documents for document owners",
                    IsActive = true
                };
                _context.ComplianceRules.Add(ownerRule);
            }

            if (departmentRule == null)
            {
                departmentRule = new ComplianceRule
                {
                    RuleName = departmentRuleName,
                    AppliesTo = "Department",
                    IsMandatory = true,
                    ValidationRules = "Require registration, tax clearance, VAT certificate and approval documents",
                    Description = "Required documents for departments",
                    IsActive = true
                };
                _context.ComplianceRules.Add(departmentRule);
            }

            var addressDocumentTypeIds = new[] { 7, 8, 9, 10 };
            foreach (var documentTypeId in addressDocumentTypeIds)
            {
                var addressRuleName = $"{addressDocumentRuleNameBase} (Type {documentTypeId})";
                var existingAddressRule = existingRules.FirstOrDefault(r => r.AppliesTo == "DocumentType" && r.RequiredDocumentTypeId == documentTypeId && r.RuleName == addressRuleName);

                if (existingAddressRule == null)
                {
                    _context.ComplianceRules.Add(new ComplianceRule
                    {
                        RuleName = addressRuleName,
                        AppliesTo = "DocumentType",
                        RequiredDocumentTypeId = documentTypeId,
                        IsMandatory = true,
                        ExpiryPeriodDays = 180,
                        ValidationRules = "Address document validity period in days",
                        Description = "Max age for address/proof of residence document type",
                        IsActive = true
                    });
                }
            }

            if (defaultDocumentRule == null)
            {
                defaultDocumentRule = new ComplianceRule
                {
                    RuleName = defaultDocumentRuleName,
                    AppliesTo = "DocumentType",
                    RequiredDocumentTypeId = null,
                    IsMandatory = true,
                    ExpiryPeriodDays = 1800,
                    ValidationRules = "Default maximum document validity period in days",
                    Description = "Fallback max age for document types without a specific rule",
                    IsActive = true
                };
                _context.ComplianceRules.Add(defaultDocumentRule);
            }

            if (warningThresholdRule == null)
            {
                warningThresholdRule = new ComplianceRule
                {
                    RuleName = warningThresholdRuleName,
                    AppliesTo = "DocumentType",
                    RequiredDocumentTypeId = null,
                    IsMandatory = true,
                    WarningThresholdDays = 30,
                    ValidationRules = "Warn when days remaining is less than this threshold",
                    Description = "Default remaining-days warning threshold for document expiry",
                    IsActive = true
                };
                _context.ComplianceRules.Add(warningThresholdRule);
            }

            if (_context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync();

            var requirementChecks = new List<ComplianceRequirement>();
            if (!await _context.ComplianceRequirements.AnyAsync(r => r.ComplianceRuleId == ownerRule.ComplianceRuleId && r.RequirementName == "Identity document"))
            {
                requirementChecks.Add(new ComplianceRequirement
                {
                    ComplianceRuleId = ownerRule.ComplianceRuleId,
                    RequirementName = "Identity document",
                    Description = "Valid photo ID is required",
                    IsMandatory = true
                });
            }

            if (!await _context.ComplianceRequirements.AnyAsync(r => r.ComplianceRuleId == ownerRule.ComplianceRuleId && r.RequirementName == "Proof of address"))
            {
                requirementChecks.Add(new ComplianceRequirement
                {
                    ComplianceRuleId = ownerRule.ComplianceRuleId,
                    RequirementName = "Proof of address",
                    Description = "Recent proof of address is required",
                    IsMandatory = true
                });
            }

            if (!await _context.ComplianceRequirements.AnyAsync(r => r.ComplianceRuleId == departmentRule.ComplianceRuleId && r.RequirementName == "Department registration"))
            {
                requirementChecks.Add(new ComplianceRequirement
                {
                    ComplianceRuleId = departmentRule.ComplianceRuleId,
                    RequirementName = "Department registration",
                    Description = "Department registration documents are required",
                    IsMandatory = true
                });
            }

            if (!await _context.ComplianceRequirements.AnyAsync(r => r.ComplianceRuleId == departmentRule.ComplianceRuleId && r.RequirementName == "Tax clearance"))
            {
                requirementChecks.Add(new ComplianceRequirement
                {
                    ComplianceRuleId = departmentRule.ComplianceRuleId,
                    RequirementName = "Tax clearance",
                    Description = "Tax clearance is required",
                    IsMandatory = true
                });
            }

            var complianceChecks = new List<ComplianceCheck>();
            if (!await _context.ComplianceChecks.AnyAsync(c => c.ComplianceRuleId == ownerRule.ComplianceRuleId && c.Notes == "Identity document verification pending"))
            {
                complianceChecks.Add(new ComplianceCheck
                {
                    ComplianceRuleId = ownerRule.ComplianceRuleId,
                    CheckStatus = "Pending",
                    Notes = "Identity document verification pending"
                });
            }

            if (!await _context.ComplianceChecks.AnyAsync(c => c.ComplianceRuleId == departmentRule.ComplianceRuleId && c.Notes == "Department compliance verification pending"))
            {
                complianceChecks.Add(new ComplianceCheck
                {
                    ComplianceRuleId = departmentRule.ComplianceRuleId,
                    CheckStatus = "Pending",
                    Notes = "Department compliance verification pending"
                });
            }

            if (requirementChecks.Any())
                _context.ComplianceRequirements.AddRange(requirementChecks);

            if (complianceChecks.Any())
                _context.ComplianceChecks.AddRange(complianceChecks);

            if (_context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync();

            _defaultComplianceRulesSeeded = true;
        }

        private async Task GenerateAlertsAsync(ComplianceStatus status)
        {
            var existingAlerts = await _context.ComplianceAlerts
                .Where(a => a.ComplianceStatusId == status.ComplianceStatusId && !a.IsResolved)
                .ToListAsync();

            // Check for expired documents
            if (status.ExpiredDocuments > 0)
            {
                if (!existingAlerts.Any(a => a.AlertType == "Expired"))
                {
                    var alert = new ComplianceAlert
                    {
                        ComplianceStatusId = status.ComplianceStatusId,
                        AlertType = "Expired",
                        Severity = "Critical",
                        AlertMessage = $"{status.ExpiredDocuments} document(s) are expired",
                        UserId = status.UserId,
                        DueDate = DateTime.UtcNow.AddDays(3),
                        RequiredAction = "Renew expired documents",
                        ActionRequiredFromUser = true
                    };

                    await CreateAlertAsync(alert);
                }
            }

            var expiringSoonCount = status.DocumentChecks.Count(dc => dc.IsExpiryValid && dc.DaysUntilExpiry.HasValue && dc.DaysUntilExpiry.Value <= 30);
            if (expiringSoonCount > 0)
            {
                if (!existingAlerts.Any(a => a.AlertType == "ExpiringSoon"))
                {
                    var alert = new ComplianceAlert
                    {
                        ComplianceStatusId = status.ComplianceStatusId,
                        AlertType = "ExpiringSoon",
                        Severity = "High",
                        AlertMessage = $"{expiringSoonCount} document(s) expire soon",
                        UserId = status.UserId,
                        DueDate = DateTime.UtcNow.AddDays(7),
                        RequiredAction = "Renew expiring documents soon",
                        ActionRequiredFromUser = true
                    };

                    await CreateAlertAsync(alert);
                }
            }

            // Check for missing documents
            if (status.MissingDocuments > 0)
            {
                if (!existingAlerts.Any(a => a.AlertType == "Missing"))
                {
                    var alert = new ComplianceAlert
                    {
                        ComplianceStatusId = status.ComplianceStatusId,
                        AlertType = "Missing",
                        Severity = "High",
                        AlertMessage = $"{status.MissingDocuments} required document(s) are missing",
                        UserId = status.UserId,
                        DueDate = DateTime.UtcNow.AddDays(7),
                        RequiredAction = "Upload missing documents",
                        ActionRequiredFromUser = true
                    };

                    await CreateAlertAsync(alert);
                }
            }

            // Check for not certified documents
            if (status.NotCertifiedDocuments > 0)
            {
                if (!existingAlerts.Any(a => a.AlertType == "NotCertified"))
                {
                    var alert = new ComplianceAlert
                    {
                        ComplianceStatusId = status.ComplianceStatusId,
                        AlertType = "NotCertified",
                        Severity = "High",
                        AlertMessage = $"{status.NotCertifiedDocuments} document(s) are not certified",
                        UserId = status.UserId,
                        DueDate = DateTime.UtcNow.AddDays(10),
                        RequiredAction = "Get documents certified and reupload",
                        ActionRequiredFromUser = true
                    };

                    await CreateAlertAsync(alert);
                }
            }
        }

        private async Task CreateAuditLogAsync(int complianceStatusId, string actionType, string description)
        {
            var performingUserId = (await _context.ComplianceStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.ComplianceStatusId == complianceStatusId))?.UserId;

            if (string.IsNullOrWhiteSpace(performingUserId))
            {
                var admin = await _userManager.FindByNameAsync("superadmin");
                if (admin != null)
                    performingUserId = admin.Id;
                else
                    performingUserId = (await _userManager.Users.AsNoTracking().FirstOrDefaultAsync())?.Id;
            }

            var log = new ComplianceAuditLog
            {
                ComplianceStatusId = complianceStatusId,
                ActionType = actionType,
                ActionDescription = description,
                PerformedBy = performingUserId ?? string.Empty,
                PerformedAt = DateTime.UtcNow,
                IsSuccessful = true
            };

            _context.ComplianceAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Null implementation for notification service (placeholder)
    /// </summary>
    public class NullNotificationService : INotificationService
    {
        public Task SendNonComplianceAlertAsync(string userId, UserComplianceDto compliance)
            => Task.CompletedTask;

        public Task SendDeadlineReminderAsync(string userId, DateTime deadline)
            => Task.CompletedTask;

        public Task SendAlert(string userId, string message, string subject)
            => Task.CompletedTask;
    }

    public interface INotificationService
    {
        Task SendNonComplianceAlertAsync(string userId, UserComplianceDto compliance);
        Task SendDeadlineReminderAsync(string userId, DateTime deadline);
        Task SendAlert(string userId, string message, string subject);
    }
}

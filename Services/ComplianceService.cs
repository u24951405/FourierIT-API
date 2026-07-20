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
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ComplianceService> _logger;
        private readonly INotificationService _notificationService;

        public ComplianceService(
            AppDbContext context,
            UserManager<User> userManager,
            ILogger<ComplianceService> logger,
            INotificationService? notificationService = null)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService ?? new NullNotificationService();
        }

        // ===== MAIN COMPLIANCE CHECK =====

        public async Task<ComplianceStatus> CheckUserComplianceAsync(string userId, bool runDetailedCheck = true)
        {
            var user = await _userManager.FindByIdAsync(userId) 
                ?? throw new Exception($"User {userId} not found");

            var status = await _context.ComplianceStatuses
                .Include(cs => cs.DocumentChecks)
                .FirstOrDefaultAsync(cs => cs.UserId == userId) 
                ?? new ComplianceStatus { UserId = userId };

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
            status.DocumentChecks.Clear();

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

                var check = await PerformDocumentCheckAsync(doc.DocumentId, status.ComplianceStatusId);
                status.DocumentChecks.Add(check);

                if (check.CheckStatus == "Compliant")
                    status.CompliantDocuments++;
                else if (check.CheckStatus == "Pending")
                    status.PendingReviewDocuments++;
                else
                {
                    status.NonCompliantDocuments++;

                    if (!check.IsExpiryValid)
                        status.ExpiredDocuments++;
                    if (!check.IsCertified)
                        status.NotCertifiedDocuments++;
                }
            }

            // Calculate scores
            status.CompliancePercentage = 
                (status.CompliantDocuments * 100) / 
                (status.TotalRequiredDocuments > 0 ? status.TotalRequiredDocuments : 1);

            status.ComplianceScore = await CalculateComplianceScoreAsync(userId);
            status.OverallRiskScore = await AssessRiskLevelAsync(userId);

            // Determine overall status
            status.OverallStatus = DetermineOverallStatus(status);
            status.RiskLevel = DetermineRiskLevel(status);

            status.LastChecked = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;
            status.NextReviewDate = DateTime.UtcNow.AddDays(30);

            // Create audit log
            await CreateAuditLogAsync(status.ComplianceStatusId, "ComplianceCheck", 
                $"Compliance check completed: {status.OverallStatus}");

            // Save
            if (status.ComplianceStatusId == 0)
                _context.ComplianceStatuses.Add(status);
            else
                _context.ComplianceStatuses.Update(status);

            await _context.SaveChangesAsync();
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
            deptStatus.CompliancePercentage = deptStatus.TotalRequiredDocuments > 0
                ? (deptStatus.CompliantDocuments * 100) / deptStatus.TotalRequiredDocuments
                : 0;

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
                CompliancePercentage = status.CompliancePercentage,
                ComplianceScore = status.ComplianceScore,
                RiskScore = status.OverallRiskScore,
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
                    DocumentName = dc.Document.FileName,
                    DocumentType = dc.Document.DocumentType.TypeName,
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
                .Where(s => s.DepartmentId == null) // Only user-level statuses
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
                CriticalRiskUsers = statuses.Count(s => s.RiskLevel == "Critical"),
                HighRiskUsers = statuses.Count(s => s.RiskLevel == "High")
            };

            dashboard.OverallCompliancePercentage = statuses.Count > 0
                ? (dashboard.CompliantUsers * 100m) / statuses.Count
                : 0;

            return dashboard;
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

        public async Task<DocumentComplianceCheck> PerformDocumentCheckAsync(int documentId, int complianceStatusId)
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
            else if (check.DaysUntilExpiry < 30)
                issues.Add($"Expiring soon ({check.DaysUntilExpiry} days)");

            // Check 2: Certification
            check.IsCertified = document.IsCertified;
            if (!check.IsCertified)
                issues.Add("Not certified copy");

            // Check 3: Recency
            var maxMonthsOld = GetMaxDocumentAge(document.DocumentTypeId);
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
                    DocumentName = dc.Document.FileName,
                    DocumentType = dc.Document.DocumentType.TypeName,
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

            check.CheckStatus = "Compliant";
            check.IsManuallyApproved = true;
            check.ManuallyReviewedBy = approvedBy;
            check.ManualReviewDate = DateTime.UtcNow;

            _context.DocumentComplianceChecks.Update(check);

            // Update compliance status
            var status = await _context.ComplianceStatuses.FindAsync(check.ComplianceStatusId);
            if (status != null)
            {
                await UpdateComplianceStatusAsync(status.ComplianceStatusId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Document check {checkId} approved by {approvedBy}");

            return true;
        }

        public async Task<bool> RejectDocumentComplianceAsync(int checkId, string rejectedBy, string reason)
        {
            var check = await _context.DocumentComplianceChecks.FindAsync(checkId)
                ?? throw new Exception($"Check {checkId} not found");

            check.CheckStatus = "Non-Compliant";
            check.NonComplianceReason = reason;
            check.ManuallyReviewedBy = rejectedBy;
            check.ManualReviewDate = DateTime.UtcNow;

            _context.DocumentComplianceChecks.Update(check);
            await _context.SaveChangesAsync();

            return true;
        }

        // ===== DEADLINES =====

        public async Task<bool> SetComplianceDeadlineAsync(string userId, DateTime deadline, string reason)
        {
            var status = await _context.ComplianceStatuses
                .FirstOrDefaultAsync(cs => cs.UserId == userId)
                ?? throw new Exception($"Compliance status for user {userId} not found");

            status.ComplianceDeadline = deadline;
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

            _context.DocumentComplianceChecks.Update(check);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<DocumentComplianceCheck>> GetPendingManualReviewsAsync()
        {
            return await _context.DocumentComplianceChecks
                .AsNoTracking()
                .Where(c => c.RequiresManualReview && !c.IsManuallyApproved)
                .Include(c => c.Document)
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

        public async Task<List<ComplianceAuditLog>> GetAuditLogsAsync(int complianceStatusId, int limit = 100)
        {
            return await _context.ComplianceAuditLogs
                .AsNoTracking()
                .Where(al => al.ComplianceStatusId == complianceStatusId)
                .OrderByDescending(al => al.PerformedAt)
                .Take(limit)
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
            status.NonCompliantDocuments = status.DocumentChecks.Count(c => c.CheckStatus != "Compliant");
            status.CompliancePercentage = status.TotalRequiredDocuments > 0
                ? (status.CompliantDocuments * 100) / status.TotalRequiredDocuments
                : 0;

            status.OverallStatus = DetermineOverallStatus(status);
            status.LastUpdated = DateTime.UtcNow;

            _context.ComplianceStatuses.Update(status);
            await _context.SaveChangesAsync();

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
            if (status.MissingDocuments > 0 && status.NonCompliantDocuments == status.TotalRequiredDocuments)
                return "Non-Compliant";

            if (status.ExpiredDocuments > 0)
                return "Non-Compliant";

            if (status.NonCompliantDocuments > 0)
                return status.ExpiredDocuments > 0 ? "Non-Compliant" : "Partial";

            if (status.PendingReviewDocuments > 0)
                return "Pending";

            return status.CompliantDocuments == status.TotalRequiredDocuments ? "Compliant" : "Partial";
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

        private int GetMaxDocumentAge(int documentTypeId)
        {
            // Address/Proof of Residence documents: 6 months
            var addressDocTypes = new[] { 7, 8, 9, 10 };
            if (addressDocTypes.Contains(documentTypeId))
                return 6;

            // ID documents: 5 years
            return 60;
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
            var log = new ComplianceAuditLog
            {
                ComplianceStatusId = complianceStatusId,
                ActionType = actionType,
                ActionDescription = description,
                PerformedBy = "System",
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

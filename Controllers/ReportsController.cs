using FourierIT_API.Data;
using FourierIT_API.DTOs.Reports;
using FourierIT_API.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Globalization;
using FourierIT_API.Services;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize(Policy = "Reports.View")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ReportPdfService _pdfService;
        private readonly DocumentValidityCalculator _documentValidityCalculator;

        public ReportsController(AppDbContext context, IConfiguration? configuration = null, DocumentValidityCalculator? documentValidityCalculator = null)
        {
            _context = context;
            _configuration = configuration ?? new ConfigurationBuilder().Build();
            _pdfService = new ReportPdfService();
            _documentValidityCalculator = documentValidityCalculator ?? new DocumentValidityCalculator();
        }

        [HttpGet("monthly")]
        public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var start = (startDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)).Date;
            var end = (endDate ?? start.AddMonths(1).AddDays(-1)).Date;
            if (end < start)
                return BadRequest(new { error = "The end date must not be before the start date." });

            var endExclusive = end.AddDays(1);
            // The chosen days are South African days: SAST midnight is 22:00 UTC the day before.
            var startUtc = start.Add(-Sast);
            var endExclusiveUtc = endExclusive.Add(-Sast);
            var documents = await _context.Documents.AsNoTracking()
                .Include(document => document.DocumentType)
                .Where(document => document.UploadedDate >= startUtc
                    && document.UploadedDate < endExclusiveUtc
                    && document.CurrentStatus != "Deleted")
                .ToListAsync();
            var allActiveDocuments = await _context.Documents.AsNoTracking()
                .Where(document => document.CurrentStatus != "Deleted")
                .Select(document => document.FileSizeBytes)
                .ToListAsync();
            var complianceStatuses = await ComplianceStatusesOfDocumentUploaders()
                .Where(status => status.LastChecked >= startUtc && status.LastChecked < endExclusiveUtc)
                .ToListAsync();
            var rangeStart = new DateTimeOffset(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc));
            var rangeEnd = new DateTimeOffset(DateTime.SpecifyKind(endExclusiveUtc, DateTimeKind.Utc));
            var auditLogs = await _context.AuditLogs.AsNoTracking()
                .Where(log => log.TimeStamp >= rangeStart && log.TimeStamp < rangeEnd)
                .ToListAsync();
            var enquiryCount = await _context.InstitutionEnquiryRequests.AsNoTracking()
                .CountAsync(request => request.RequestDate >= rangeStart && request.RequestDate < rangeEnd);

            var days = Enumerable.Range(0, (end - start).Days + 1).Select(offset => start.AddDays(offset)).ToList();
            var uploadGroups = documents.GroupBy(document => document.UploadedDate.Add(Sast).Date)
                .ToDictionary(group => group.Key, group => group.Count());
            var uploadVolume = days.Select(day => new MonthlyUploadVolumeDto
            {
                Day = day.Day,
                Count = uploadGroups.GetValueOrDefault(day, 0)
            }).ToList();
            var totalUploads = documents.Count;
            // With no uploads there is no peak day (0), rather than "day 1".
            var peak = uploadVolume.Where(item => item.Count > 0).OrderByDescending(item => item.Count).FirstOrDefault();

            var distributionGroups = documents.GroupBy(document => document.DocumentType?.TypeName ?? "Unknown")
                .OrderBy(group => group.Key).ToList();
            var distribution = distributionGroups.Select((group, index) => new MonthlyDistributionCategoryDto
            {
                Label = group.Key,
                Count = group.Count(),
                Percentage = totalUploads == 0 ? 0 : Math.Round(group.Count() * 100m / totalUploads, 2),
                Color = new[] { "#1e2a3a", "#2d5282", "#4a7fb5", "#7bafd4", "#b0cfe8" }[index % 5]
            }).ToList();

            var totalCapacityBytes = _configuration.GetValue<long?>("Storage:TotalCapacityBytes") ?? 1_073_741_824L;
            var usedBytes = allActiveDocuments.Sum();
            var usedGb = usedBytes / 1_073_741_824m;
            var totalGb = totalCapacityBytes / 1_073_741_824m;
            var availableGb = Math.Max(0, totalGb - usedGb);

            return Ok(new MonthlyReportDto
            {
                ReportId = $"DV-OPR-{start:yyyyMMdd}-{end:yyyyMMdd}",
                Month = start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                DateGenerated = DateTime.UtcNow,
                CreatedBy = User?.Identity?.Name ?? "System",
                Processing = new MonthlyProcessingStatsDto
                {
                    Verified = documents.Count(document => IsStatus(document.CurrentStatus, "approved", "verified", "compliant")),
                    PendingVerification = documents.Count(document => IsStatus(document.CurrentStatus, "pending", "under review", "awaiting verification")),
                    Rejected = documents.Count(document => document.CurrentStatus.Contains("Reject", StringComparison.OrdinalIgnoreCase)),
                    // Documents flagged by a Compliance Officer and owners flagged as suspicious or sanctioned; resolved flags don't count.
                    FlaggedAnomalies = complianceStatuses.Count(status => status.IsSuspicious || status.HasSanctionFlag)
                        + auditLogs.Count(log => log.ActionCode == "DOCUMENT_FLAGGED"),
                    PartOfEnquiry = enquiryCount
                },
                SecurityEvents = days.Select(day =>
                {
                    var dayLogs = auditLogs.Where(log => log.TimeStamp.ToOffset(Sast).Date == day).ToList();
                    return new MonthlySecurityEventDto
                    {
                        Day = day.Day,
                        FailedLogins = dayLogs.Count(log => log.ActionCode == "LOGIN_FAILURE"),
                        // Changes to what someone is allowed to do.
                        PermissionElevationRequest = dayLogs.Count(log => log.ActionCode is "USER_ROLE_CHANGED" or "DEPARTMENT_ADMIN_ASSIGNED" or "DEPARTMENT_ADMIN_UNASSIGNED"),
                        // Documents flagged, and document access taken away. (Everyday activity such as logins and uploads is not a security event.)
                        UnusualAccessPattern = dayLogs.Count(log => log.ActionCode is "DOCUMENT_FLAGGED" or "DOCUMENT_ACCESS_REVOKED" or "REQUEST_REVOKED")
                    };
                }).ToList(),
                Distribution = distribution,
                Storage = new MonthlyStorageStatsDto
                {
                    UsedGb = Math.Round(usedGb, 2),
                    AvailableGb = Math.Round(availableGb, 2),
                    TotalGb = Math.Round(totalGb, 2),
                    UsedPercentage = totalGb == 0 ? 0 : Math.Round(usedGb * 100m / totalGb, 2)
                },
                UploadVolume = uploadVolume,
                TotalUploads = totalUploads,
                DailyAverage = days.Count == 0 ? 0 : Math.Round(totalUploads / (decimal)days.Count, 2),
                PeakDay = peak?.Day ?? 0
            });
        }

        [HttpGet("monthly/pdf")]
        public async Task<IActionResult> DownloadMonthlyPdf([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var result = await GetMonthlyReport(startDate, endDate);
            if (result.Result is not OkObjectResult ok || ok.Value is not MonthlyReportDto report)
                return result.Result ?? BadRequest(new { error = "Monthly report could not be generated." });
            return File(_pdfService.GenerateMonthly(report), "application/pdf", $"{report.ReportId}.pdf");
        }

        private static bool IsStatus(string status, params string[] expected)
        {
            return expected.Any(value => status.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        [HttpGet("activity/{ownerId}")]
        public async Task<ActionResult<ActivityReportDto>> GetActivityReport(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return BadRequest(new { error = "Owner ID is required." });

            var owner = await _context.Users.AsNoTracking()
                .Include(user => user.Profile)
                .FirstOrDefaultAsync(user => user.Id == ownerId);

            if (owner == null)
                return NotFound(new { error = "Document owner not found." });

            var now = DateTimeOffset.UtcNow;
            var documents = await _context.Documents.AsNoTracking()
                .Include(document => document.DocumentType)
                .Where(document => document.UserId == ownerId && document.CurrentStatus != "Deleted")
                .OrderByDescending(document => document.UploadedDate)
                .ToListAsync();

            // One colour per document type, shared by the chart and the inventory table.
            var documentColors = new[] { "#1e2a3a", "#2d5282", "#4a7fb5", "#7bafd4", "#b0cfe8" };
            var typeNames = documents.Select(TypeNameOf).Distinct().OrderBy(name => name).ToList();
            var colorByType = typeNames.Select((name, index) => (name, color: documentColors[index % documentColors.Length]))
                .ToDictionary(item => item.name, item => item.color);

            var inventory = documents
                .Select(document => new DocumentInventoryItemDto
                {
                    DocumentName = document.FileName,
                    Category = TypeNameOf(document),
                    CategoryColor = colorByType[TypeNameOf(document)],
                    UploadDate = SastDate(document.UploadedDate),
                    ExpiryDate = document.ExpiryDate != default ? document.ExpiryDate.ToOffset(Sast).ToString("yyyy-MM-dd") : null,
                    VerificationStatus = ResolveDocumentVerificationStatus(document.CurrentStatus, document.DocumentType, document.ExpiryDate)
                })
                .ToList();

            var categoryBreakdown = documents
                .GroupBy(TypeNameOf)
                .OrderBy(group => group.Key)
                .Select(group => new MonthlyDistributionCategoryDto
                {
                    Label = group.Key,
                    Count = group.Count(),
                    Percentage = Math.Round(group.Count() * 100m / documents.Count, 2),
                    Color = colorByType[group.Key]
                })
                .ToList();

            var activeDocuments = documents.Count(document => ResolveDocumentStatusState(document.CurrentStatus, document.DocumentType, document.ExpiryDate) == "Active");

            // ── Who has had the owner's documents ──
            var approvals = await _context.DocumentAccessApprovals.AsNoTracking()
                .Where(approval => approval.Document.UserId == ownerId)
                .Select(approval => new
                {
                    approval.ApprovalId,
                    approval.EnquiryRequestId,
                    approval.DocumentId,
                    approval.ExpiresAt,
                    approval.IsRevoked,
                    InstitutionName = approval.InstitutionEnquiryRequest.Institution.InstitutionName
                })
                .ToListAsync();

            // 1. Views and downloads inside DocuVault (the owner, Compliance Officers, admins).
            var inAppLogs = await _context.DocumentAccessLogs.AsNoTracking()
                .Include(log => log.Document)
                .Include(log => log.AccessedByUser)
                    .ThenInclude(user => user.Profile)
                .Where(log => log.Document.UserId == ownerId)
                .OrderByDescending(log => log.AccessDateTime)
                .Take(ActivityLogLimit)
                .ToListAsync();

            var userIds = inAppLogs.Select(log => log.AccessedByUserId).Distinct().ToList();
            var rolesByUserId = (await _context.UserRoles.AsNoTracking()
                    .Where(userRole => userIds.Contains(userRole.UserId))
                    .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id,
                        (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? "User" })
                    .ToListAsync())
                .GroupBy(role => role.UserId)
                .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(role => role.RoleName).OrderBy(name => name)));

            var accessEvents = inAppLogs.Select(log =>
            {
                var isOwner = log.AccessedByUserId == ownerId;
                var fileName = log.Document?.FileName ?? "a document";
                return (When: new DateTimeOffset(DateTime.SpecifyKind(log.AccessDateTime, DateTimeKind.Utc)), Entry: new VaultAccessLogEntryDto
                {
                    AccessorName = BuildFullName(log.AccessedByUser?.Profile),
                    AccessorRole = isOwner ? "Document owner" : rolesByUserId.GetValueOrDefault(log.AccessedByUserId, "User"),
                    ActionReason = $"{DescribeAccess(log.ActionType)} {fileName}",
                    Organisation = "DocuVault"
                });
            }).ToList();

            // 2. Downloads by institutions, which are recorded in the audit trail rather than the access log.
            var approvalIds = approvals.Select(approval => approval.ApprovalId).ToList();
            var targetedRequestIds = await _context.InstitutionEnquiryRequests.AsNoTracking()
                .Where(request => request.TargetUserId == ownerId)
                .Select(request => request.EnquiryRequestId)
                .ToListAsync();
            var requestIds = targetedRequestIds.Union(approvals.Select(approval => approval.EnquiryRequestId)).ToList();

            var institutionDownloads = await _context.AuditLogs.AsNoTracking()
                .Include(log => log.Institution)
                .Where(log => (log.ActionCode == "INSTITUTION_DOCUMENT_DOWNLOADED" || log.ActionCode == "INSTITUTION_DOCUMENT_VIEWED") && log.RecordID != null
                    && ((log.TableAffected == "DocumentAccessApprovals" && approvalIds.Contains(log.RecordID.Value))
                        || (log.TableAffected == "InstitutionEnquiryRequests" && requestIds.Contains(log.RecordID.Value))))
                .OrderByDescending(log => log.TimeStamp)
                .Take(ActivityLogLimit)
                .ToListAsync();

            var ownerFileNames = documents.Select(document => document.FileName).ToList();
            foreach (var log in institutionDownloads)
            {
                // A department request can cover several people: keep request-level downloads only when they are this owner's file.
                if (log.TableAffected == "InstitutionEnquiryRequests" && !targetedRequestIds.Contains(log.RecordID!.Value)
                    && !ownerFileNames.Any(name => (log.Description ?? string.Empty).Contains($"\"{name}\"")))
                    continue;

                var institutionName = log.Institution?.InstitutionName ?? "An institution";
                accessEvents.Add((log.TimeStamp, new VaultAccessLogEntryDto
                {
                    AccessorName = institutionName,
                    AccessorRole = "Institution",
                    ActionReason = log.Description ?? "Downloaded a shared document.",
                    Organisation = institutionName
                }));
            }

            var vaultAccessLog = accessEvents
                .OrderByDescending(item => item.When)
                .Take(ActivityLogLimit)
                .Select(item => { item.Entry.Timestamp = item.When.ToOffset(Sast).ToString("yyyy-MM-dd HH:mm"); return item.Entry; })
                .ToList();

            // ── Institutions the owner has shared documents with (approved requests only) ──
            var clientRelationships = approvals
                .GroupBy(approval => approval.InstitutionName)
                .Select(group => new ClientRelationshipDto
                {
                    Organisation = group.Key,
                    DocumentsShared = group.Select(approval => approval.DocumentId).Distinct().Count(),
                    Status = group.Any(approval => !approval.IsRevoked && (approval.ExpiresAt == null || approval.ExpiresAt > now.UtcDateTime))
                        ? "Active"
                        : group.All(approval => approval.IsRevoked) ? "Revoked" : "Expired"
                })
                .OrderBy(item => item.Status == "Active" ? 0 : 1)
                .ThenByDescending(item => item.DocumentsShared)
                .ToList();

            var compliance = await _context.ComplianceStatuses.AsNoTracking()
                .FirstOrDefaultAsync(status => status.UserId == ownerId);

            var activityReport = new ActivityReportDto
            {
                ReportId = $"DV-DAR-{ownerId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                DateGenerated = DateTime.UtcNow,
                DocumentOwner = BuildFullName(owner.Profile),
                OwnerId = owner.Id,
                ComplianceStatus = compliance?.OverallStatus,
                CompliancePercentage = compliance?.CompliancePercentage,
                ActiveDocuments = activeDocuments,
                InactiveDocuments = documents.Count - activeDocuments,
                TotalDocuments = documents.Count,
                DistributionByCategory = categoryBreakdown,
                Inventory = inventory,
                VaultAccessLog = vaultAccessLog,
                ClientRelationships = clientRelationships
            };

            return Ok(activityReport);
        }

        private const int ActivityLogLimit = 50;

        /// <summary>DocuVault's users are in South Africa, so report times are shown in SAST (UTC+2).</summary>
        private static readonly TimeSpan Sast = TimeSpan.FromHours(2);

        private static string SastDate(DateTime utc) =>
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(Sast).ToString("yyyy-MM-dd");

        private static string TypeNameOf(Document document) => document.DocumentType?.TypeName ?? "Unknown type";

        private static string DescribeAccess(string actionType) => actionType switch
        {
            "Download" => "Downloaded",
            "AdminDownload" => "Downloaded for review:",
            "AdminPreview" => "Viewed for review:",
            _ => "Viewed"
        };

        [HttpGet("activity/{ownerId}/pdf")]
        public async Task<IActionResult> DownloadActivityPdf(string ownerId)
        {
            var result = await GetActivityReport(ownerId);
            if (result.Result is not OkObjectResult ok || ok.Value is not ActivityReportDto report)
                return result.Result ?? BadRequest(new { error = "Activity report could not be generated." });
            return File(_pdfService.GenerateActivity(report), "application/pdf", $"{report.ReportId}.pdf");
        }

        private static string BuildFullName(Profile? profile)
        {
            if (profile == null)
                return "Unknown User";

            var name = $"{profile.FirstName ?? string.Empty} {profile.LastName ?? string.Empty}".Trim();
            return string.IsNullOrWhiteSpace(name) ? "Unknown User" : name;
        }

        private string ResolveDocumentVerificationStatus(string currentStatus, DocumentType? documentType, DateTimeOffset expiryDate)
        {
            var normalized = currentStatus?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return "Pending";

            if (normalized.Equals("Verified", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Compliant", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTimeOffset.UtcNow;
                if (expiryDate <= now)
                    return "Expired";

                return _documentValidityCalculator.IsExpiringSoon(documentType, expiryDate, now) ? "Expiring Soon" : "Verified";
            }

            if (normalized.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("Review", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("Awaiting", StringComparison.OrdinalIgnoreCase))
                return "Pending";

            // A rejected document is never "Verified", however far off its expiry date is.
            if (normalized.Contains("Reject", StringComparison.OrdinalIgnoreCase))
                return "Rejected";

            if (normalized.Equals("Expired", StringComparison.OrdinalIgnoreCase) || expiryDate <= DateTimeOffset.UtcNow)
                return "Expired";

            return _documentValidityCalculator.IsExpiringSoon(documentType, expiryDate, DateTimeOffset.UtcNow) ? "Expiring Soon" : "Verified";
        }

        private string ResolveDocumentStatusState(string currentStatus, DocumentType? documentType, DateTimeOffset expiryDate)
        {
            var verification = ResolveDocumentVerificationStatus(currentStatus, documentType, expiryDate);
            return verification == "Verified" || verification == "Expiring Soon" ? "Active" : "Inactive";
        }

        [HttpPost("ad-hoc")]
        public async Task<ActionResult<object>> CreateAdHocReport([FromBody] AdHocReportRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.FocusAreas.Count == 0)
                return BadRequest(new { error = "A title and at least one report focus area are required." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? "unknown";
            var createdByName = User.FindFirstValue(ClaimTypes.GivenName)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? "Unknown User";

            var report = new AdHocReport
            {
                Title = request.Title.Trim(),
                DateFrom = request.DateFrom.Date,
                DateTo = request.DateTo.Date,
                FocusAreas = JsonSerializer.Serialize(request.FocusAreas),
                ExportFormat = string.IsNullOrWhiteSpace(request.ExportFormat) ? "PDF" : request.ExportFormat.Trim().ToUpperInvariant(),
                CreatedByUserId = userId,
                CreatedByName = createdByName,
                CreatedAt = DateTime.UtcNow,
                Status = "ready",
                SizeKb = 0
            };

            _context.AdHocReports.Add(report);
            await _context.SaveChangesAsync();

            return Ok(new { reportId = report.AdHocReportId.ToString(), message = "Ad-hoc report saved." });
        }

        [HttpGet("ad-hoc/recent")]
        public async Task<ActionResult<List<AdHocReportSummaryDto>>> GetRecentAdHocReports()
        {
            var reports = await _context.AdHocReports
                .AsNoTracking()
                .OrderByDescending(report => report.CreatedAt)
                .Take(10)
                .Select(report => new AdHocReportSummaryDto
                {
                    Id = report.AdHocReportId.ToString(),
                    Title = report.Title,
                    Date = report.CreatedAt,
                    SizeKb = report.SizeKb,
                    Status = report.Status
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("ad-hoc/{id:int}")]
        public async Task<ActionResult<AdHocReportDataDto>> GetAdHocReportData(int id)
        {
            var report = await _context.AdHocReports.AsNoTracking()
                .FirstOrDefaultAsync(item => item.AdHocReportId == id);
            if (report == null)
                return NotFound(new { error = "Report not found." });

            var focusAreas = JsonSerializer.Deserialize<List<string>>(report.FocusAreas) ?? new();
            var normalizedFocusAreas = focusAreas
                .Select(focus => focus.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var result = new AdHocReportDataDto
            {
                ReportId = report.AdHocReportId,
                Title = report.Title,
                DateFrom = report.DateFrom,
                DateTo = report.DateTo,
                DateGenerated = report.CreatedAt,
                CreatedBy = report.CreatedByName,
                FocusAreas = focusAreas
            };

            var hasDocuments = normalizedFocusAreas.Contains("DOCUMENT_PROCESSING")
                || normalizedFocusAreas.Contains("DOCUMENT_DISTRIBUTION")
                || normalizedFocusAreas.Contains("SYSTEM_STORAGE")
                || normalizedFocusAreas.Contains("UPLOAD_VOLUME");
            var documents = hasDocuments ? await GetReportDocumentsAsync(report) : new List<Document>();

            if (normalizedFocusAreas.Any(focus => focus is "COMPLIANCE" or "COMPLIANCE_STATUS" or "COMPLIANCE_STATUSES"))
            {
                result.ComplianceResults = await BuildAdHocComplianceResultsAsync(report);
            }

            if (normalizedFocusAreas.Contains("DOCUMENT_PROCESSING"))
            {
                result.DocumentResults = documents.Select(document => new AdHocDocumentResultDto
                {
                    DocumentId = document.DocumentId,
                    FileName = document.FileName,
                    DocumentType = document.DocumentType?.TypeName ?? "Unknown",
                    Status = document.CurrentStatus,
                    UploadedDate = document.UploadedDate,
                    FileSizeBytes = document.FileSizeBytes,
                    Certified = document.IsCertified
                }).ToList();
            }

            if (normalizedFocusAreas.Contains("DOCUMENT_DISTRIBUTION"))
            {
                result.DistributionResults = documents
                    .GroupBy(document => document.DocumentType?.TypeName ?? "Unknown")
                    .OrderBy(group => group.Key)
                    .Select(group => new AdHocDistributionResultDto
                    {
                        DocumentType = group.Key,
                        DocumentCount = group.Count(),
                        TotalSizeBytes = group.Sum(document => document.FileSizeBytes)
                    })
                    .ToList();
            }

            if (normalizedFocusAreas.Contains("SYSTEM_STORAGE"))
            {
                result.StorageResult = new AdHocStorageResultDto
                {
                    DateFrom = report.DateFrom,
                    DateTo = report.DateTo,
                    DocumentCount = documents.Count,
                    TotalSizeBytes = documents.Sum(document => document.FileSizeBytes)
                };
            }

            if (normalizedFocusAreas.Contains("UPLOAD_VOLUME"))
            {
                result.UploadVolumeResults = documents
                    .GroupBy(document => document.UploadedDate.Date)
                    .OrderBy(group => group.Key)
                    .Select(group => new AdHocUploadVolumeResultDto
                    {
                        UploadDate = group.Key,
                        UploadCount = group.Count()
                    })
                    .ToList();
            }

            if (normalizedFocusAreas.Contains("SECURITY_ANOMALIES"))
            {
                result.SecurityResults = await BuildAdHocSecurityResultsAsync(report);
            }

            return Ok(result);
        }

        /// <summary>
        /// Events that matter for security: failed logins, changes to what someone may do, flags, revoked access and removed accounts.
        /// Everyday activity (logins, uploads, views) is not an anomaly.
        /// </summary>
        private static readonly string[] SecurityActionCodes =
        {
            "LOGIN_FAILURE", "USER_ROLE_CHANGED", "DEPARTMENT_ADMIN_ASSIGNED", "DEPARTMENT_ADMIN_UNASSIGNED",
            "DOCUMENT_FLAGGED", "DOCUMENT_ACCESS_REVOKED", "REQUEST_REVOKED", "USER_DELETED", "EMAIL_CHANGED"
        };

        private async Task<List<AdHocComplianceResultDto>> BuildAdHocComplianceResultsAsync(AdHocReport report)
        {
            var statuses = await ComplianceStatusesOfDocumentUploaders()
                .Where(status => status.LastChecked >= report.DateFrom && status.LastChecked < report.DateTo.Date.AddDays(1))
                .OrderBy(status => status.LastChecked)
                .ToListAsync();
            var names = await NamesByUserIdAsync(statuses.Select(status => status.UserId));

            return statuses.Select(status => new AdHocComplianceResultDto
            {
                StatusId = status.ComplianceStatusId,
                UserId = status.UserId,
                Name = names.GetValueOrDefault(status.UserId, "Unknown user"),
                OverallStatus = status.OverallStatus,
                RiskLevel = status.RiskLevel,
                CompliancePercentage = status.CompliancePercentage,
                OverallRiskScore = status.OverallRiskScore,
                LastChecked = DateTime.SpecifyKind(status.LastChecked, DateTimeKind.Utc).Add(Sast)
            }).ToList();
        }

        private async Task<List<AdHocSecurityResultDto>> BuildAdHocSecurityResultsAsync(AdHocReport report)
        {
            var from = new DateTimeOffset(DateTime.SpecifyKind(report.DateFrom.Date, DateTimeKind.Utc)).Add(-Sast);
            var to = new DateTimeOffset(DateTime.SpecifyKind(report.DateTo.Date.AddDays(1), DateTimeKind.Utc)).Add(-Sast);
            var logs = await _context.AuditLogs.AsNoTracking()
                .Include(log => log.Institution)
                .Where(log => log.TimeStamp >= from && log.TimeStamp < to && SecurityActionCodes.Contains(log.ActionCode))
                .OrderBy(log => log.TimeStamp)
                .ToListAsync();
            var names = await NamesByUserIdAsync(logs.Where(log => log.UserId != null).Select(log => log.UserId!));

            return logs.Select(log => new AdHocSecurityResultDto
            {
                AuditLogId = log.AuditLogId,
                UserId = log.UserId ?? "System",
                Name = log.UserId != null ? names.GetValueOrDefault(log.UserId, "Unknown user") : log.Institution?.InstitutionName ?? "System",
                Action = log.ActionCode,
                Timestamp = log.TimeStamp.ToOffset(Sast).DateTime,
                Description = log.Description ?? string.Empty,
                Table = log.TableAffected
            }).ToList();
        }

        private async Task<Dictionary<string, string>> NamesByUserIdAsync(IEnumerable<string> userIds)
        {
            var ids = userIds.Distinct().ToList();
            var users = await _context.Users.AsNoTracking()
                .Where(user => ids.Contains(user.Id))
                .Select(user => new { user.Id, user.Email, FirstName = user.Profile != null ? user.Profile.FirstName : null, LastName = user.Profile != null ? user.Profile.LastName : null })
                .ToListAsync();
            return users.ToDictionary(user => user.Id, user =>
            {
                var name = $"{user.FirstName} {user.LastName}".Trim();
                return string.IsNullOrWhiteSpace(name) ? user.Email ?? "Unknown user" : name;
            });
        }

        [HttpGet("ad-hoc/{id:int}/pdf")]
        public async Task<IActionResult> DownloadAdHocPdf(int id)
        {
            var result = await GetAdHocReportData(id);
            if (result.Result is not OkObjectResult ok || ok.Value is not AdHocReportDataDto report)
                return result.Result ?? BadRequest(new { error = "Ad-hoc report could not be generated." });
            return File(_pdfService.GenerateAdHoc(report), "application/pdf", $"AD-HOC-{report.ReportId}.pdf");
        }

        [HttpGet("ad-hoc/{id:int}/excel")]
        public async Task<IActionResult> DownloadAdHocExcel(int id)
        {
            var report = await _context.AdHocReports
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.AdHocReportId == id);
            if (report == null)
                return NotFound(new { error = "Report not found." });

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Document Activity");
            sheet.Cell("A1").Value = report.Title;
            sheet.Range("A1:G1").Merge();
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("A1").Style.Font.FontSize = 16;

            sheet.Cell("A3").Value = "Field";
            sheet.Cell("B3").Value = "Value";
            sheet.Range("A3:B3").Style.Font.Bold = true;
            sheet.Range("A3:B3").Style.Fill.BackgroundColor = XLColor.LightBlue;
            sheet.Cell("A4").Value = "Report ID";
            sheet.Cell("B4").Value = report.AdHocReportId;
            sheet.Cell("A5").Value = "Generated by";
            sheet.Cell("B5").Value = report.CreatedByName;
            sheet.Cell("A6").Value = "Generated date";
            sheet.Cell("B6").Value = report.CreatedAt;
            sheet.Cell("B6").Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            sheet.Cell("A7").Value = "Date from";
            sheet.Cell("B7").Value = report.DateFrom;
            sheet.Cell("B7").Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell("A8").Value = "Date to";
            sheet.Cell("B8").Value = report.DateTo;
            sheet.Cell("B8").Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell("A9").Value = "Export format";
            sheet.Cell("B9").Value = report.ExportFormat;
            sheet.Cell("A10").Value = "Focus areas";
            sheet.Cell("B10").Value = string.Join(", ", JsonSerializer.Deserialize<List<string>>(report.FocusAreas) ?? new());

            var focusAreas = JsonSerializer.Deserialize<List<string>>(report.FocusAreas) ?? new();
            if (focusAreas.Count == 0)
                focusAreas.Add("DOCUMENT_PROCESSING");

            foreach (var focusArea in focusAreas.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                switch (focusArea.Trim().ToUpperInvariant())
                {
                    case "COMPLIANCE":
                    case "COMPLIANCE_STATUS":
                    case "COMPLIANCE_STATUSES":
                        await AddComplianceResultsSheetAsync(workbook, report);
                        break;
                    case "SECURITY_ANOMALIES":
                        await AddSecurityResultsSheetAsync(workbook, report);
                        break;
                    case "DOCUMENT_DISTRIBUTION":
                        await AddDistributionResultsSheetAsync(workbook, report);
                        break;
                    case "SYSTEM_STORAGE":
                        await AddStorageResultsSheetAsync(workbook, report);
                        break;
                    case "UPLOAD_VOLUME":
                        await AddUploadVolumeResultsSheetAsync(workbook, report);
                        break;
                    default:
                        await AddDocumentResultsSheetAsync(workbook, report);
                        break;
                }
            }

            sheet.Columns().AdjustToContents();
            sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 20);
            sheet.Column(2).Width = Math.Min(Math.Max(sheet.Column(2).Width, 24), 80);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{SanitizeFileName(report.Title)}.xlsx");
        }

        private static string SanitizeFileName(string title)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var safeTitle = new string(title.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(safeTitle) ? "ad-hoc-report" : safeTitle.Trim();
        }

        private async Task AddDocumentResultsSheetAsync(XLWorkbook workbook, AdHocReport report)
        {
            var documents = await GetReportDocumentsAsync(report);
            var sheet = CreateResultsSheet(workbook, "Document Processing", new[] { "Document ID", "File name", "Document type", "Status", "Uploaded date", "File size (bytes)", "Certified" });
            for (var index = 0; index < documents.Count; index++)
            {
                var document = documents[index];
                var row = index + 2;
                sheet.Cell(row, 1).Value = document.DocumentId;
                sheet.Cell(row, 2).Value = document.FileName;
                sheet.Cell(row, 3).Value = document.DocumentType?.TypeName ?? "Unknown";
                sheet.Cell(row, 4).Value = document.CurrentStatus;
                sheet.Cell(row, 5).Value = document.UploadedDate;
                sheet.Cell(row, 5).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                sheet.Cell(row, 6).Value = document.FileSizeBytes;
                sheet.Cell(row, 7).Value = document.IsCertified;
            }
            FormatResultsSheet(sheet);
        }

        private async Task<List<Document>> GetReportDocumentsAsync(AdHocReport report)
        {
            return await _context.Documents.AsNoTracking()
                .Include(document => document.DocumentType)
                .Where(document => document.UploadedDate >= report.DateFrom
                    && document.UploadedDate < report.DateTo.Date.AddDays(1)
                    && document.CurrentStatus != "Deleted")
                .OrderBy(document => document.UploadedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Compliance records of people who upload documents. Stakeholders and Compliance Officers are left out,
        /// including any record left over from before a role change.
        /// </summary>
        private IQueryable<ComplianceStatus> ComplianceStatusesOfDocumentUploaders()
        {
            var uploaderRoleNames = ComplianceService.DocumentUploaderRoles.Select(role => role.ToUpperInvariant()).ToList();
            var uploaderIds = _context.UserRoles
                .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.NormalizedName })
                .Where(joined => joined.NormalizedName != null && uploaderRoleNames.Contains(joined.NormalizedName))
                .Select(joined => joined.UserId);

            return _context.ComplianceStatuses.AsNoTracking().Where(status => uploaderIds.Contains(status.UserId));
        }

        private async Task AddComplianceResultsSheetAsync(XLWorkbook workbook, AdHocReport report)
        {
            var results = await BuildAdHocComplianceResultsAsync(report);
            var sheet = CreateResultsSheet(workbook, "Compliance Results", new[] { "Document owner", "Overall status", "Risk level", "Compliance %", "Risk score", "Last checked (SAST)" });
            for (var index = 0; index < results.Count; index++)
            {
                var item = results[index];
                var row = index + 2;
                sheet.Cell(row, 1).Value = item.Name;
                sheet.Cell(row, 2).Value = item.OverallStatus;
                sheet.Cell(row, 3).Value = item.RiskLevel;
                sheet.Cell(row, 4).Value = item.CompliancePercentage;
                sheet.Cell(row, 5).Value = item.OverallRiskScore;
                sheet.Cell(row, 6).Value = item.LastChecked;
                sheet.Cell(row, 6).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            }
            FormatResultsSheet(sheet);
        }

        private async Task AddSecurityResultsSheetAsync(XLWorkbook workbook, AdHocReport report)
        {
            var results = await BuildAdHocSecurityResultsAsync(report);
            var sheet = CreateResultsSheet(workbook, "Security Anomalies", new[] { "When (SAST)", "Who", "Action", "Description" });
            for (var index = 0; index < results.Count; index++)
            {
                var item = results[index];
                var row = index + 2;
                sheet.Cell(row, 1).Value = item.Timestamp;
                sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                sheet.Cell(row, 2).Value = item.Name;
                sheet.Cell(row, 3).Value = item.Action;
                sheet.Cell(row, 4).Value = item.Description;
            }
            FormatResultsSheet(sheet);
        }

        private async Task AddDistributionResultsSheetAsync(XLWorkbook workbook, AdHocReport report)
        {
            var documents = await GetReportDocumentsAsync(report);
            var groups = documents.GroupBy(document => document.DocumentType?.TypeName ?? "Unknown")
                .OrderBy(group => group.Key).ToList();
            var sheet = CreateResultsSheet(workbook, "Document Distribution", new[] { "Document type", "Document count", "Total size (bytes)" });
            for (var index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                sheet.Cell(index + 2, 1).Value = group.Key;
                sheet.Cell(index + 2, 2).Value = group.Count();
                sheet.Cell(index + 2, 3).Value = group.Sum(document => document.FileSizeBytes);
            }
            FormatResultsSheet(sheet);
        }

        private async Task AddStorageResultsSheetAsync(XLWorkbook workbook, AdHocReport report)
        {
            var documents = await GetReportDocumentsAsync(report);
            var sheet = CreateResultsSheet(workbook, "System Storage", new[] { "Period from", "Period to", "Document count", "Total size (bytes)" });
            sheet.Cell("A2").Value = report.DateFrom;
            sheet.Cell("B2").Value = report.DateTo;
            sheet.Cell("A2").Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell("B2").Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell("C2").Value = documents.Count;
            sheet.Cell("D2").Value = documents.Sum(document => document.FileSizeBytes);
            FormatResultsSheet(sheet);
        }

        private async Task AddUploadVolumeResultsSheetAsync(XLWorkbook workbook, AdHocReport report)
        {
            var documents = await GetReportDocumentsAsync(report);
            var groups = documents.GroupBy(document => document.UploadedDate.Date).OrderBy(group => group.Key).ToList();
            var sheet = CreateResultsSheet(workbook, "Upload Volume", new[] { "Upload date", "Upload count" });
            for (var index = 0; index < groups.Count; index++)
            {
                sheet.Cell(index + 2, 1).Value = groups[index].Key;
                sheet.Cell(index + 2, 1).Style.DateFormat.Format = "yyyy-mm-dd";
                sheet.Cell(index + 2, 2).Value = groups[index].Count();
            }
            FormatResultsSheet(sheet);
        }

        private static IXLWorksheet CreateResultsSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers)
        {
            var sheet = workbook.Worksheets.Add(name);
            for (var index = 0; index < headers.Count; index++) sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Range(1, 1, 1, headers.Count).Style.Font.Bold = true;
            sheet.Range(1, 1, 1, headers.Count).Style.Fill.BackgroundColor = XLColor.LightBlue;
            return sheet;
        }

        private static void FormatResultsSheet(IXLWorksheet sheet)
        {
            sheet.Columns().AdjustToContents();
            for (var column = 1; column <= sheet.LastColumnUsed()?.ColumnNumber(); column++)
                sheet.Column(column).Width = Math.Min(Math.Max(sheet.Column(column).Width, 14), 60);
        }

        [HttpGet("document-owner-compliance")]
        public async Task<ActionResult<List<DocumentOwnerComplianceReportRowDto>>> GetDocumentOwnerComplianceReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? departmentId,
            [FromQuery] int? institutionId,
            [FromQuery] string? complianceStatus,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildDocumentOwnerComplianceRowsAsync(startDate, endDate, departmentId, institutionId, complianceStatus);
            var sorted = SortDocumentOwnerRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        [HttpGet("institution-document-requests")]
        public async Task<ActionResult<List<InstitutionDocumentRequestReportRowDto>>> GetInstitutionDocumentRequestReport(
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            [FromQuery] int? institutionId,
            [FromQuery] int? departmentId,
            [FromQuery] string? status,
            [FromQuery] string? recipientType,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildInstitutionDocumentRequestRowsAsync(startDate, endDate, institutionId, departmentId, status, recipientType);
            var sorted = SortInstitutionRequestRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        [HttpGet("department-compliance")]
        public async Task<ActionResult<List<DepartmentComplianceReportRowDto>>> GetDepartmentComplianceReport(
            [FromQuery] int? institutionId,
            [FromQuery] int? departmentId,
            [FromQuery] string? complianceStatus,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildDepartmentComplianceRowsAsync(institutionId, departmentId, complianceStatus);
            var sorted = SortDepartmentRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        [HttpGet("control-break/document-requests-by-institution")]
        public async Task<ActionResult<List<InstitutionRequestControlBreakGroupDto>>> GetDocumentRequestsByInstitution(
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            [FromQuery] int? institutionId,
            [FromQuery] int? departmentId,
            [FromQuery] string? status,
            [FromQuery] string? recipientType)
        {
            var rows = await BuildInstitutionDocumentRequestRowsAsync(startDate, endDate, institutionId, departmentId, status, recipientType);

            var groups = rows
                .GroupBy(r => new { r.InstitutionId, r.Institution })
                .Select(g => new InstitutionRequestControlBreakGroupDto
                {
                    InstitutionId = g.Key.InstitutionId,
                    Institution = g.Key.Institution,
                    TotalRequests = g.Count(),
                    Approved = g.Count(x => IsApprovedStatus(x.Status)),
                    Pending = g.Count(x => IsPendingStatus(x.Status)),
                    Denied = g.Count(x => IsDeniedStatus(x.Status)),
                    Requests = g.OrderByDescending(x => x.RequestDate).ToList()
                })
                .OrderBy(g => g.Institution)
                .ToList();

            return Ok(groups);
        }

        [HttpGet("control-break/compliance-by-department")]
        public async Task<ActionResult<List<DepartmentComplianceControlBreakGroupDto>>> GetComplianceByDepartment(
            [FromQuery] int? institutionId,
            [FromQuery] int? departmentId,
            [FromQuery] string? complianceStatus)
        {
            var departmentRows = await BuildDepartmentComplianceRowsAsync(institutionId, departmentId, complianceStatus);
            var ownerRows = await BuildDocumentOwnerComplianceRowsAsync(null, null, departmentId, institutionId, complianceStatus);

            var groups = departmentRows
                .OrderBy(d => d.Department)
                .Select(d =>
                {
                    var deptOwners = ownerRows
                        .Where(o => o.DepartmentId == d.DepartmentId)
                        .Select(o =>
                        {
                            var denominator = o.UploadedDocuments + o.MissingDocuments;
                            var pct = denominator == 0 ? 0m : Math.Round((decimal)o.UploadedDocuments * 100m / denominator, 2);
                            return new DepartmentComplianceControlBreakDetailDto
                            {
                                DocumentOwner = o.DocumentOwner,
                                ComplianceStatus = o.ComplianceStatus,
                                UploadedDocuments = o.UploadedDocuments,
                                MissingDocuments = o.MissingDocuments,
                                CompliancePercentage = pct,
                                RiskRating = o.RiskRating
                            };
                        })
                        .OrderBy(x => x.DocumentOwner)
                        .ToList();

                    return new DepartmentComplianceControlBreakGroupDto
                    {
                        DepartmentId = d.DepartmentId,
                        Department = d.Department,
                        DepartmentAdmin = d.DepartmentAdmin,
                        Institution = d.Institution,
                        RequiredDocuments = d.RequiredDocuments,
                        UploadedDocuments = d.UploadedDocuments,
                        MissingDocuments = d.MissingDocuments,
                        CompliancePercentage = d.CompliancePercentage,
                        Details = deptOwners
                    };
                })
                .ToList();

            return Ok(groups);
        }

        [HttpGet("department-document-inventory")]
        public async Task<ActionResult<List<DepartmentDocumentInventoryReportRowDto>>> GetDepartmentDocumentInventoryReport(
            [FromQuery] int? institutionId,
            [FromQuery] int? departmentId,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildDepartmentDocumentInventoryRowsAsync(institutionId, departmentId);
            var sorted = SortDepartmentInventoryRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        [HttpGet("institution-access-history")]
        public async Task<ActionResult<List<InstitutionAccessHistoryReportRowDto>>> GetInstitutionAccessHistoryReport(
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            [FromQuery] int? institutionId,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildInstitutionAccessHistoryRowsAsync(startDate, endDate, institutionId, status);
            var sorted = SortInstitutionAccessHistoryRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        [HttpGet("expiring-documents")]
        public async Task<ActionResult<List<ExpiringDocumentsReportRowDto>>> GetExpiringDocumentsReport(
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            [FromQuery] int? departmentId,
            [FromQuery] int? institutionId,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildExpiringDocumentsRowsAsync(startDate, endDate, departmentId, institutionId);
            var sorted = SortExpiringDocumentsRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        [HttpGet("outstanding-compliance")]
        public async Task<ActionResult<List<OutstandingComplianceReportRowDto>>> GetOutstandingComplianceReport(
            [FromQuery] int? institutionId,
            [FromQuery] int? departmentId,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection)
        {
            var rows = await BuildOutstandingComplianceRowsAsync(institutionId, departmentId);
            var sorted = SortOutstandingComplianceRows(rows, sortBy, sortDirection);
            return Ok(sorted);
        }

        private async Task<List<DocumentOwnerComplianceReportRowDto>> BuildDocumentOwnerComplianceRowsAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? departmentId,
            int? institutionId,
            string? complianceStatus)
        {
            var docOwnerRole = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.NormalizedName == "DOCUMENT OWNER"
                    || (r.Name != null && r.Name.ToUpper() == "DOCUMENT OWNER"));

            if (docOwnerRole == null)
            {
                docOwnerRole = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => (r.NormalizedName != null && r.NormalizedName.Contains("DOCUMENT") && r.NormalizedName.Contains("OWNER"))
                        || (r.Name != null && r.Name.Contains("Document") && r.Name.Contains("Owner")));
            }

            if (docOwnerRole == null)
            {
                return new List<DocumentOwnerComplianceReportRowDto>();
            }

            var ownerUserIds = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.RoleId == docOwnerRole.Id)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            if (ownerUserIds.Count == 0)
            {
                return new List<DocumentOwnerComplianceReportRowDto>();
            }

            var owners = await _context.Users
                .AsNoTracking()
                .Where(u => ownerUserIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    FirstName = u.Profile != null ? u.Profile.FirstName : string.Empty,
                    LastName = u.Profile != null ? u.Profile.LastName : string.Empty,
                    Email = u.Email ?? string.Empty,
                    DepartmentId = u.DepartmentId,
                    DepartmentName = u.Department != null ? u.Department.DepartmentName : string.Empty,
                    InstitutionId = u.Department != null ? (int?)u.Department.Branch.InstitutionId : null,
                    InstitutionName = u.Department != null ? u.Department.Branch.Institution.InstitutionName : string.Empty,
                    ComplianceStatus = u.ComplianceStatus != null ? u.ComplianceStatus.OverallStatus : "Pending",
                    CheckedPercentage = u.ComplianceStatus != null ? (int?)u.ComplianceStatus.CompliancePercentage : null,
                    RiskLevel = u.ComplianceStatus != null ? u.ComplianceStatus.RiskLevel : null,
                    EntityTypeId = u.EntityTypeId,
                    EntityTypeName = u.EntityType != null ? u.EntityType.Name : string.Empty
                })
                .ToListAsync();

            if (departmentId.HasValue)
            {
                owners = owners.Where(o => o.DepartmentId == departmentId.Value).ToList();
            }

            if (institutionId.HasValue)
            {
                owners = owners.Where(o => o.InstitutionId == institutionId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(complianceStatus))
            {
                var normalized = complianceStatus.Trim().ToLowerInvariant();
                owners = owners.Where(o => o.ComplianceStatus.ToLower() == normalized).ToList();
            }

            var filteredUserIds = owners.Select(o => o.Id).ToList();
            if (filteredUserIds.Count == 0)
            {
                return new List<DocumentOwnerComplianceReportRowDto>();
            }

            var documents = await _context.Documents
                .AsNoTracking()
                .Where(d => filteredUserIds.Contains(d.UserId) && d.CurrentStatus != "Deleted")
                .Select(d => new { d.UserId, d.DocumentTypeId, d.UploadedDate, d.CurrentStatus, d.ExpiryDate })
                .ToListAsync();

            // A rejected or expired document doesn't meet a requirement, so only valid ones count as "uploaded".
            var nowUtc = DateTimeOffset.UtcNow;
            var userUploadDocTypes = documents
                .Where(d => !d.CurrentStatus.Contains("Reject", StringComparison.OrdinalIgnoreCase)
                    && !d.CurrentStatus.Equals("Expired", StringComparison.OrdinalIgnoreCase)
                    && d.ExpiryDate > nowUtc)
                .GroupBy(d => d.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.DocumentTypeId).Distinct().ToHashSet());

            var userLastUpload = documents
                .GroupBy(d => d.UserId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.UploadedDate));

            var entityTypeIds = owners
                .Where(o => o.EntityTypeId.HasValue)
                .Select(o => o.EntityTypeId!.Value)
                .Distinct()
                .ToList();

            var entityRequired = await _context.RequiredDocuments
                .AsNoTracking()
                .Where(rd => rd.IsMandatory && entityTypeIds.Contains(rd.EntityTypeId))
                .GroupBy(rd => rd.EntityTypeId)
                .Select(g => new { EntityTypeId = g.Key, DocumentTypeIds = g.Select(x => x.DocumentTypeId).Distinct().ToList() })
                .ToListAsync();

            var entityRequiredMap = entityRequired.ToDictionary(
                x => x.EntityTypeId,
                x => x.DocumentTypeIds);

            var departmentIds = owners
                .Where(o => o.DepartmentId.HasValue)
                .Select(o => o.DepartmentId!.Value)
                .Distinct()
                .ToList();

            var departmentRequired = await _context.DepartmentDocumentTypes
                .AsNoTracking()
                .Where(ddt => departmentIds.Contains(ddt.DepartmentId))
                .GroupBy(ddt => ddt.DepartmentId)
                .Select(g => new { DepartmentId = g.Key, DocumentTypeIds = g.Select(x => x.DocumentTypeId).Distinct().ToList() })
                .ToListAsync();

            var departmentRequiredMap = departmentRequired.ToDictionary(
                x => x.DepartmentId,
                x => x.DocumentTypeIds);

            var allRelevantDocTypeIds = entityRequiredMap.Values.SelectMany(v => v)
                .Concat(departmentRequiredMap.Values.SelectMany(v => v))
                .Distinct()
                .ToList();

            var documentTypeNames = await _context.DocumentTypes
                .AsNoTracking()
                .Where(dt => allRelevantDocTypeIds.Contains(dt.DocumentTypeId))
                .ToDictionaryAsync(dt => dt.DocumentTypeId, dt => dt.TypeName);

            var rows = new List<DocumentOwnerComplianceReportRowDto>(owners.Count);

            foreach (var owner in owners)
            {
                var requiredDocTypeIds = new List<int>();

                if (owner.EntityTypeId.HasValue && entityRequiredMap.TryGetValue(owner.EntityTypeId.Value, out var entityIds) && entityIds.Count > 0)
                {
                    requiredDocTypeIds = entityIds;
                }
                else if (owner.DepartmentId.HasValue && departmentRequiredMap.TryGetValue(owner.DepartmentId.Value, out var deptIds) && deptIds.Count > 0)
                {
                    requiredDocTypeIds = deptIds;
                }

                var uploadedIds = userUploadDocTypes.TryGetValue(owner.Id, out var uploaded)
                    ? uploaded
                    : new HashSet<int>();

                var matchedUploaded = requiredDocTypeIds.Count == 0
                    ? uploadedIds.Count
                    : requiredDocTypeIds.Count(id => uploadedIds.Contains(id));

                var totalRequired = requiredDocTypeIds.Count;
                var missingIds = requiredDocTypeIds.Where(id => !uploadedIds.Contains(id)).ToList();
                var missingNames = missingIds
                    .Select(id => documentTypeNames.TryGetValue(id, out var name) ? name : $"Document Type {id}")
                    .ToArray();

                var lastUpload = userLastUpload.TryGetValue(owner.Id, out var uploadedAt)
                    ? uploadedAt
                    : (DateTime?)null;

                // Match the Compliance Status column: use the compliance check's percentage once one has run.
                var compliancePct = owner.CheckedPercentage.HasValue
                    ? owner.CheckedPercentage.Value
                    : totalRequired == 0 ? 100m : Math.Round((decimal)matchedUploaded * 100m / totalRequired, 2);

                rows.Add(new DocumentOwnerComplianceReportRowDto
                {
                    DocumentOwner = BuildFullName(owner.FirstName, owner.LastName),
                    Email = owner.Email,
                    EntityType = owner.EntityTypeName,
                    ComplianceStatus = owner.ComplianceStatus,
                    UploadedDocuments = matchedUploaded,
                    MissingDocuments = missingIds.Count,
                    LastUploadDate = lastUpload,
                    CompliancePercentage = compliancePct,
                    RiskRating = ResolveRiskRating(owner.RiskLevel, missingIds.Count, totalRequired),
                    DepartmentId = owner.DepartmentId ?? 0,
                    Department = owner.DepartmentName,
                    InstitutionId = owner.InstitutionId ?? 0,
                    Institution = owner.InstitutionName,
                    MissingDocumentNames = missingNames
                });
            }

            if (startDate.HasValue)
            {
                rows = rows.Where(r => r.LastUploadDate.HasValue && r.LastUploadDate.Value.Date >= startDate.Value.Date).ToList();
            }

            if (endDate.HasValue)
            {
                rows = rows.Where(r => r.LastUploadDate.HasValue && r.LastUploadDate.Value.Date <= endDate.Value.Date).ToList();
            }

            return rows;
        }

        private async Task<List<InstitutionDocumentRequestReportRowDto>> BuildInstitutionDocumentRequestRowsAsync(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            int? institutionId,
            int? departmentId,
            string? status,
            string? recipientType)
        {
            var query = _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(r => r.RequestDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.RequestDate <= endDate.Value);
            }

            if (institutionId.HasValue)
            {
                query = query.Where(r => r.InstitutionId == institutionId.Value);
            }

            if (departmentId.HasValue)
            {
                query = query.Where(r => r.TargetDepartmentId == departmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToLowerInvariant();
                query = query.Where(r => r.Status.ToLower() == normalizedStatus);
            }

            if (!string.IsNullOrWhiteSpace(recipientType))
            {
                var normalizedType = recipientType.Trim().ToLowerInvariant();
                query = query.Where(r => r.RequestType.ToLower() == normalizedType);
            }

            var baseRows = await query
                .Select(r => new InstitutionDocumentRequestReportRowDto
                {
                    RequestId = r.EnquiryRequestId,
                    Institution = r.Institution.InstitutionName,
                    Recipient = r.RequestType == "Department"
                        ? (r.TargetDepartment != null ? r.TargetDepartment.DepartmentName : "Unknown Department")
                        : ((r.TargetUser != null ? r.TargetUser.Profile.FirstName : "") + " " + (r.TargetUser != null ? r.TargetUser.Profile.LastName : "")).Trim(),
                    RecipientType = r.RequestType,
                    RequestDate = r.RequestDate,
                    SubmissionDeadline = r.SubmissionDeadline,
                    ReferenceNumber = r.ReferenceNumber ?? string.Empty,
                    RequestJustification = r.PurposeNote ?? string.Empty,
                    Status = r.Status,
                    ApprovalDenialDate = r.RespondedAt,
                    InstitutionId = r.InstitutionId,
                    DepartmentId = r.TargetDepartmentId
                })
                .ToListAsync();

            if (baseRows.Count == 0)
            {
                return baseRows;
            }

            var requestIds = baseRows.Select(r => r.RequestId).ToList();
            var docPairs = await _context.InstitutionRequestedDocumentTypes
                .AsNoTracking()
                .Where(rdt => requestIds.Contains(rdt.EnquiryRequestId))
                .Select(rdt => new
                {
                    rdt.EnquiryRequestId,
                    Name = rdt.DocumentType.TypeName
                })
                .ToListAsync();

            var docsByRequest = docPairs
                .GroupBy(x => x.EnquiryRequestId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Name).Distinct().OrderBy(n => n).ToArray());

            foreach (var row in baseRows)
            {
                row.RequestedDocuments = docsByRequest.TryGetValue(row.RequestId, out var docs)
                    ? docs
                    : Array.Empty<string>();
            }

            return baseRows;
        }

        private async Task<List<DepartmentComplianceReportRowDto>> BuildDepartmentComplianceRowsAsync(
            int? institutionId,
            int? departmentId,
            string? complianceStatus)
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    InstitutionId = d.Branch.InstitutionId,
                    InstitutionName = d.Branch.Institution.InstitutionName
                })
                .ToListAsync();

            if (institutionId.HasValue)
            {
                departments = departments.Where(d => d.InstitutionId == institutionId.Value).ToList();
            }

            if (departmentId.HasValue)
            {
                departments = departments.Where(d => d.DepartmentId == departmentId.Value).ToList();
            }

            if (departments.Count == 0)
            {
                return new List<DepartmentComplianceReportRowDto>();
            }

            var departmentIds = departments.Select(d => d.DepartmentId).ToList();

            var departmentAdmins = await _context.UserRoles
                .AsNoTracking()
                .Join(_context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole, role })
                .Where(joined => joined.role.NormalizedName == "DEPARTMENT ADMIN")
                .Join(_context.Users,
                    joined => joined.userRole.UserId,
                    user => user.Id,
                    (joined, user) => new { joined.userRole, user })
                .Where(joined => joined.user.DepartmentId.HasValue && departmentIds.Contains(joined.user.DepartmentId.Value))
                .Select(joined => new
                {
                    DepartmentId = joined.user.DepartmentId!.Value,
                    FirstName = joined.user.Profile.FirstName,
                    LastName = joined.user.Profile.LastName,
                    joined.user.Email
                })
                .ToListAsync();

            var adminByDepartment = departmentAdmins
                .GroupBy(a => a.DepartmentId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        var fullName = BuildFullName(first.FirstName, first.LastName);
                        return string.IsNullOrWhiteSpace(fullName) ? first.Email ?? "Unassigned" : fullName;
                    });

            var requiredByDepartment = await _context.DepartmentDocumentTypes
                .AsNoTracking()
                .Where(ddt => departmentIds.Contains(ddt.DepartmentId))
                .GroupBy(ddt => ddt.DepartmentId)
                .Select(g => new
                {
                    DepartmentId = g.Key,
                    DocumentTypeIds = g.Select(x => x.DocumentTypeId).Distinct().ToList()
                })
                .ToListAsync();

            var requiredMap = requiredByDepartment.ToDictionary(x => x.DepartmentId, x => x.DocumentTypeIds);

            var userDepartmentMap = await _context.Users
                .AsNoTracking()
                .Where(u => u.DepartmentId.HasValue && departmentIds.Contains(u.DepartmentId.Value))
                .Select(u => new { u.Id, DepartmentId = u.DepartmentId!.Value })
                .ToListAsync();

            var userIdToDepartment = userDepartmentMap.ToDictionary(x => x.Id, x => x.DepartmentId);
            var relevantUserIds = userIdToDepartment.Keys.ToList();

            var docs = await _context.Documents
                .AsNoTracking()
                .Where(d => relevantUserIds.Contains(d.UserId) && d.CurrentStatus != "Deleted")
                .Select(d => new { d.UserId, d.DocumentTypeId })
                .ToListAsync();

            var uploadedByDepartment = docs
                .Where(d => userIdToDepartment.ContainsKey(d.UserId))
                .GroupBy(d => userIdToDepartment[d.UserId])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.DocumentTypeId).Distinct().ToHashSet());

            var riskByDepartment = await _context.ComplianceStatuses
                .AsNoTracking()
                .Where(cs => cs.User.DepartmentId.HasValue && departmentIds.Contains(cs.User.DepartmentId.Value))
                .GroupBy(cs => cs.User.DepartmentId!.Value)
                .Select(g => new
                {
                    DepartmentId = g.Key,
                    AverageRisk = g.Average(x => (decimal?)x.OverallRiskScore) ?? 0m
                })
                .ToListAsync();

            var riskMap = riskByDepartment.ToDictionary(x => x.DepartmentId, x => x.AverageRisk);

            var rows = new List<DepartmentComplianceReportRowDto>(departments.Count);

            foreach (var department in departments)
            {
                var requiredIds = requiredMap.TryGetValue(department.DepartmentId, out var req)
                    ? req
                    : new List<int>();

                var uploadedIds = uploadedByDepartment.TryGetValue(department.DepartmentId, out var uploaded)
                    ? uploaded
                    : new HashSet<int>();

                var uploadedRequiredCount = requiredIds.Count(id => uploadedIds.Contains(id));
                var requiredCount = requiredIds.Count;
                var missingCount = Math.Max(requiredCount - uploadedRequiredCount, 0);
                var compliancePct = requiredCount == 0
                    ? 0m
                    : Math.Round((decimal)uploadedRequiredCount * 100m / requiredCount, 2);

                var avgRisk = riskMap.TryGetValue(department.DepartmentId, out var r) ? r : 0m;

                rows.Add(new DepartmentComplianceReportRowDto
                {
                    DepartmentId = department.DepartmentId,
                    Department = department.DepartmentName,
                    DepartmentAdmin = adminByDepartment.TryGetValue(department.DepartmentId, out var admin)
                        ? admin
                        : "Unassigned",
                    RequiredDocuments = requiredCount,
                    UploadedDocuments = uploadedRequiredCount,
                    MissingDocuments = missingCount,
                    CompliancePercentage = compliancePct,
                    RiskRating = ResolveRiskFromScore(avgRisk),
                    InstitutionId = department.InstitutionId,
                    Institution = department.InstitutionName
                });
            }

            if (!string.IsNullOrWhiteSpace(complianceStatus))
            {
                var normalized = complianceStatus.Trim().ToLowerInvariant();
                rows = rows.Where(r => ResolveComplianceStatus(r.CompliancePercentage).ToLower() == normalized).ToList();
            }

            return rows;
        }

        private async Task<List<DepartmentDocumentInventoryReportRowDto>> BuildDepartmentDocumentInventoryRowsAsync(
            int? institutionId,
            int? departmentId)
        {
            var departmentRows = await BuildDepartmentComplianceRowsAsync(institutionId, departmentId, null);
            var departmentIds = departmentRows.Select(d => d.DepartmentId).ToList();

            if (departmentIds.Count == 0)
            {
                return new List<DepartmentDocumentInventoryReportRowDto>();
            }

            var requiredByDepartment = await _context.DepartmentDocumentTypes
                .AsNoTracking()
                .Where(ddt => departmentIds.Contains(ddt.DepartmentId))
                .Select(ddt => new
                {
                    ddt.DepartmentId,
                    ddt.DocumentTypeId,
                    ddt.IsMandatory,
                    DocumentTypeName = ddt.DocumentType.TypeName
                })
                .ToListAsync();

            var userDepartmentMap = await _context.Users
                .AsNoTracking()
                .Where(u => u.DepartmentId.HasValue && departmentIds.Contains(u.DepartmentId.Value))
                .Select(u => new { u.Id, DepartmentId = u.DepartmentId!.Value })
                .ToListAsync();

            var relevantUserIds = userDepartmentMap.Select(u => u.Id).ToList();
            var docs = await _context.Documents
                .AsNoTracking()
                .Where(d => relevantUserIds.Contains(d.UserId) && d.CurrentStatus != "Deleted")
                .Select(d => new { d.UserId, d.DocumentTypeId, d.UploadedDate })
                .ToListAsync();

            var uploadedByUser = docs
                .GroupBy(d => d.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.DocumentTypeId).Distinct().ToHashSet());

            var lastUploadByUser = docs
                .GroupBy(d => d.UserId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.UploadedDate));

            var rows = new List<DepartmentDocumentInventoryReportRowDto>();

            foreach (var department in departmentRows)
            {
                var requirements = requiredByDepartment
                    .Where(r => r.DepartmentId == department.DepartmentId)
                    .OrderBy(r => r.DocumentTypeName)
                    .ToList();

                var departmentUploadedIds = userDepartmentMap
                    .Where(u => u.DepartmentId == department.DepartmentId)
                    .Select(u => u.Id)
                    .Where(id => uploadedByUser.TryGetValue(id, out var set))
                    .SelectMany(id => uploadedByUser[id])
                    .Distinct()
                    .ToHashSet();

                foreach (var requirement in requirements)
                {
                    var uploaded = departmentUploadedIds.Contains(requirement.DocumentTypeId);
                    var uploadDate = userDepartmentMap
                        .Where(u => u.DepartmentId == department.DepartmentId)
                        .Select(u => u.Id)
                        .Where(userId => uploadedByUser.TryGetValue(userId, out var set) && set.Contains(requirement.DocumentTypeId))
                        .Select(userId => lastUploadByUser.TryGetValue(userId, out var dt) ? dt : (DateTime?)null)
                        .Where(dt => dt.HasValue)
                        .OrderByDescending(dt => dt)
                        .FirstOrDefault();

                    rows.Add(new DepartmentDocumentInventoryReportRowDto
                    {
                        DepartmentId = department.DepartmentId,
                        Department = department.Department,
                        DepartmentAdmin = department.DepartmentAdmin,
                        Institution = department.Institution,
                        RequiredDocument = requirement.DocumentTypeName,
                        IsUploaded = uploaded,
                        UploadDate = uploadDate,
                        RequiredDocuments = requirements.Count,
                        UploadedDocuments = requirements.Count(r => departmentUploadedIds.Contains(r.DocumentTypeId)),
                        MissingDocuments = requirements.Count - requirements.Count(r => departmentUploadedIds.Contains(r.DocumentTypeId)),
                        CompliancePercentage = department.CompliancePercentage,
                        RiskRating = department.RiskRating
                    });
                }
            }

            return rows;
        }

        private async Task<List<InstitutionAccessHistoryReportRowDto>> BuildInstitutionAccessHistoryRowsAsync(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            int? institutionId,
            string? status)
        {
            // Access exists only once a request was approved (it may since have been revoked). Pending and denied requests never gave access.
            var query = _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Where(r => r.Status == "Approved" || r.Status == "Revoked");

            if (startDate.HasValue)
            {
                query = query.Where(r => r.RespondedAt >= startDate.Value.UtcDateTime);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.RespondedAt <= endDate.Value.UtcDateTime);
            }

            if (institutionId.HasValue)
            {
                query = query.Where(r => r.InstitutionId == institutionId.Value);
            }

            var requests = await query
                .Select(r => new
                {
                    r.EnquiryRequestId,
                    Institution = r.Institution.InstitutionName,
                    Recipient = r.RequestType == "Department"
                        ? (r.TargetDepartment != null ? r.TargetDepartment.DepartmentName : "Unknown Department")
                        : ((r.TargetUser != null ? r.TargetUser.Profile.FirstName : "") + " " + (r.TargetUser != null ? r.TargetUser.Profile.LastName : "")).Trim(),
                    r.RequestType,
                    r.RespondedAt,
                    r.Status,
                    AccessExpiry = r.AccessToken != null ? (DateTimeOffset?)r.AccessToken.ExpiryTimeStamp : null,
                    TokenRevoked = r.AccessToken != null && r.AccessToken.IsRevoked
                })
                .ToListAsync();

            // Downloads are in the audit trail; each one names its request ("... under request #12.").
            var requestIds = requests.Select(r => r.EnquiryRequestId).ToHashSet();
            var downloadLogs = await _context.AuditLogs.AsNoTracking()
                .Where(log => log.ActionCode == "INSTITUTION_DOCUMENT_DOWNLOADED" || log.ActionCode == "INSTITUTION_DOCUMENT_VIEWED")
                .Select(log => log.Description)
                .ToListAsync();
            var downloadsByRequest = downloadLogs
                .Select(description => System.Text.RegularExpressions.Regex.Match(description ?? string.Empty, "^(?:Downloaded|Viewed) \"(.+)\" under request #(\\d+)"))
                .Where(match => match.Success && requestIds.Contains(int.Parse(match.Groups[2].Value)))
                .GroupBy(match => int.Parse(match.Groups[2].Value))
                .ToDictionary(group => group.Key, group => group.Select(match => match.Groups[1].Value).Distinct().OrderBy(name => name).ToArray());

            var now = DateTimeOffset.UtcNow;
            var rows = requests.Select(r => new InstitutionAccessHistoryReportRowDto
            {
                RequestId = r.EnquiryRequestId,
                Institution = r.Institution,
                Recipient = string.IsNullOrWhiteSpace(r.Recipient) ? "Unknown" : r.Recipient,
                RecipientType = r.RequestType,
                AccessGrantedDate = r.RespondedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(r.RespondedAt.Value, DateTimeKind.Utc)) : null,
                AccessExpiry = r.AccessExpiry,
                AccessStatus = r.Status == "Revoked" || r.TokenRevoked
                    ? "Revoked"
                    : r.AccessExpiry.HasValue && r.AccessExpiry.Value <= now ? "Expired" : "Active",
                DocumentsAccessed = downloadsByRequest.GetValueOrDefault(r.EnquiryRequestId, Array.Empty<string>())
            }).ToList();

            if (!string.IsNullOrWhiteSpace(status))
            {
                rows = rows.Where(r => r.AccessStatus.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return rows;
        }

        private async Task<List<ExpiringDocumentsReportRowDto>> BuildExpiringDocumentsRowsAsync(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            int? departmentId,
            int? institutionId)
        {
            var query = _context.Documents
                .AsNoTracking()
                .Include(d => d.User)
                .ThenInclude(u => u.Profile)
                .Include(d => d.DocumentType)
                .Include(d => d.User)
                .ThenInclude(u => u.Department)
                .ThenInclude(dep => dep!.Branch)
                .ThenInclude(b => b!.Institution)
                .Where(d => d.CurrentStatus != "Deleted" && !d.CurrentStatus.Contains("Reject"));

            if (departmentId.HasValue)
            {
                query = query.Where(d => d.User.DepartmentId == departmentId.Value);
            }

            if (institutionId.HasValue)
            {
                query = query.Where(d => d.User.Department != null && d.User.Department.Branch.InstitutionId == institutionId.Value);
            }

            var candidates = await query
                .Select(d => new
                {
                    Owner = d.User.Profile != null
                        ? (d.User.Profile.FirstName + " " + d.User.Profile.LastName).Trim()
                        : (d.User.Email ?? d.User.UserName ?? d.User.Id),
                    // Only Department Admins belong to a department; document owners don't.
                    Department = d.User.Department != null ? d.User.Department.DepartmentName : "No department",
                    DocumentType = d.DocumentType.TypeName,
                    d.ExpiryDate,
                    d.DocumentType.WarningDays
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var rows = candidates
                .Select(c => new
                {
                    Row = new ExpiringDocumentsReportRowDto
                    {
                        Owner = c.Owner,
                        Department = c.Department,
                        DocumentType = c.DocumentType,
                        ExpiryDate = c.ExpiryDate,
                        DaysRemaining = (int)Math.Floor((c.ExpiryDate.UtcDateTime - now).TotalDays)
                    },
                    WarningDays = c.WarningDays > 0 ? c.WarningDays : 30
                })
                // Without a chosen end date, "near expiry" means inside the document type's warning period.
                .Where(x => endDate.HasValue || x.Row.DaysRemaining <= x.WarningDays)
                .Select(x => x.Row)
                .ToList();

            if (startDate.HasValue)
            {
                rows = rows.Where(r => r.ExpiryDate >= startDate.Value).ToList();
            }

            if (endDate.HasValue)
            {
                rows = rows.Where(r => r.ExpiryDate <= endDate.Value).ToList();
            }

            return rows
                .Where(r => r.DaysRemaining >= 0)
                .OrderBy(r => r.DaysRemaining)
                .ToList();
        }

        private async Task<List<OutstandingComplianceReportRowDto>> BuildOutstandingComplianceRowsAsync(
            int? institutionId,
            int? departmentId)
        {
            var ownerRows = await BuildDocumentOwnerComplianceRowsAsync(null, null, departmentId, institutionId, null);
            var departmentRows = await BuildDepartmentComplianceRowsAsync(institutionId, departmentId, null);

            var results = new List<OutstandingComplianceReportRowDto>();

            foreach (var owner in ownerRows.Where(o => o.MissingDocuments > 0 || o.ComplianceStatus != "Compliant"))
            {
                results.Add(new OutstandingComplianceReportRowDto
                {
                    EntityType = "Document Owner",
                    Name = owner.DocumentOwner,
                    Department = owner.Department,
                    Institution = owner.Institution,
                    MissingDocuments = owner.MissingDocuments,
                    CompliancePercentage = owner.CompliancePercentage,
                    RiskRating = owner.RiskRating,
                    OutstandingRequirements = owner.MissingDocumentNames
                });
            }

            foreach (var department in departmentRows.Where(d => d.MissingDocuments > 0 || d.CompliancePercentage < 100m))
            {
                results.Add(new OutstandingComplianceReportRowDto
                {
                    EntityType = "Department",
                    Name = department.Department,
                    Department = department.Department,
                    Institution = department.Institution,
                    MissingDocuments = department.MissingDocuments,
                    CompliancePercentage = department.CompliancePercentage,
                    RiskRating = department.RiskRating,
                    OutstandingRequirements = Array.Empty<string>()
                });
            }

            return results
                .OrderByDescending(r => r.MissingDocuments)
                .ToList();
        }

        private static List<DepartmentDocumentInventoryReportRowDto> SortDepartmentInventoryRows(
            List<DepartmentDocumentInventoryReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "department").Trim().ToLowerInvariant();

            return key switch
            {
                "institution" => descending ? rows.OrderByDescending(r => r.Institution).ToList() : rows.OrderBy(r => r.Institution).ToList(),
                "departmentadmin" => descending ? rows.OrderByDescending(r => r.DepartmentAdmin).ToList() : rows.OrderBy(r => r.DepartmentAdmin).ToList(),
                "requireddocument" => descending ? rows.OrderByDescending(r => r.RequiredDocument).ToList() : rows.OrderBy(r => r.RequiredDocument).ToList(),
                "uploadeddocuments" => descending ? rows.OrderByDescending(r => r.UploadedDocuments).ToList() : rows.OrderBy(r => r.UploadedDocuments).ToList(),
                "missingdocuments" => descending ? rows.OrderByDescending(r => r.MissingDocuments).ToList() : rows.OrderBy(r => r.MissingDocuments).ToList(),
                "compliancepercentage" => descending ? rows.OrderByDescending(r => r.CompliancePercentage).ToList() : rows.OrderBy(r => r.CompliancePercentage).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.Department).ToList() : rows.OrderBy(r => r.Department).ToList()
            };
        }

        private static List<InstitutionAccessHistoryReportRowDto> SortInstitutionAccessHistoryRows(
            List<InstitutionAccessHistoryReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "accessgranteddate").Trim().ToLowerInvariant();

            return key switch
            {
                "institution" => descending ? rows.OrderByDescending(r => r.Institution).ToList() : rows.OrderBy(r => r.Institution).ToList(),
                "recipient" => descending ? rows.OrderByDescending(r => r.Recipient).ToList() : rows.OrderBy(r => r.Recipient).ToList(),
                "recipienttype" => descending ? rows.OrderByDescending(r => r.RecipientType).ToList() : rows.OrderBy(r => r.RecipientType).ToList(),
                "accessexpiry" => descending ? rows.OrderByDescending(r => r.AccessExpiry).ToList() : rows.OrderBy(r => r.AccessExpiry).ToList(),
                "accessstatus" => descending ? rows.OrderByDescending(r => r.AccessStatus).ToList() : rows.OrderBy(r => r.AccessStatus).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.AccessGrantedDate).ToList() : rows.OrderBy(r => r.AccessGrantedDate).ToList()
            };
        }

        private static List<ExpiringDocumentsReportRowDto> SortExpiringDocumentsRows(
            List<ExpiringDocumentsReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "daysremaining").Trim().ToLowerInvariant();

            return key switch
            {
                "owner" => descending ? rows.OrderByDescending(r => r.Owner).ToList() : rows.OrderBy(r => r.Owner).ToList(),
                "department" => descending ? rows.OrderByDescending(r => r.Department).ToList() : rows.OrderBy(r => r.Department).ToList(),
                "documenttype" => descending ? rows.OrderByDescending(r => r.DocumentType).ToList() : rows.OrderBy(r => r.DocumentType).ToList(),
                "expirydate" => descending ? rows.OrderByDescending(r => r.ExpiryDate).ToList() : rows.OrderBy(r => r.ExpiryDate).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.DaysRemaining).ToList() : rows.OrderBy(r => r.DaysRemaining).ToList()
            };
        }

        private static List<OutstandingComplianceReportRowDto> SortOutstandingComplianceRows(
            List<OutstandingComplianceReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "missingdocuments").Trim().ToLowerInvariant();

            return key switch
            {
                "entitytype" => descending ? rows.OrderByDescending(r => r.EntityType).ToList() : rows.OrderBy(r => r.EntityType).ToList(),
                "name" => descending ? rows.OrderByDescending(r => r.Name).ToList() : rows.OrderBy(r => r.Name).ToList(),
                "compliancepercentage" => descending ? rows.OrderByDescending(r => r.CompliancePercentage).ToList() : rows.OrderBy(r => r.CompliancePercentage).ToList(),
                "riskrating" => descending ? rows.OrderByDescending(r => r.RiskRating).ToList() : rows.OrderBy(r => r.RiskRating).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.MissingDocuments).ToList() : rows.OrderBy(r => r.MissingDocuments).ToList()
            };
        }

        private static List<DocumentOwnerComplianceReportRowDto> SortDocumentOwnerRows(
            List<DocumentOwnerComplianceReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "documentOwner").Trim().ToLowerInvariant();

            return key switch
            {
                "email" => descending ? rows.OrderByDescending(r => r.Email).ToList() : rows.OrderBy(r => r.Email).ToList(),
                "entitytype" => descending ? rows.OrderByDescending(r => r.EntityType).ToList() : rows.OrderBy(r => r.EntityType).ToList(),
                "compliancestatus" => descending ? rows.OrderByDescending(r => r.ComplianceStatus).ToList() : rows.OrderBy(r => r.ComplianceStatus).ToList(),
                "uploadeddocuments" => descending ? rows.OrderByDescending(r => r.UploadedDocuments).ToList() : rows.OrderBy(r => r.UploadedDocuments).ToList(),
                "missingdocuments" => descending ? rows.OrderByDescending(r => r.MissingDocuments).ToList() : rows.OrderBy(r => r.MissingDocuments).ToList(),
                "lastuploaddate" => descending ? rows.OrderByDescending(r => r.LastUploadDate).ToList() : rows.OrderBy(r => r.LastUploadDate).ToList(),
                "compliancepercentage" => descending ? rows.OrderByDescending(r => r.CompliancePercentage).ToList() : rows.OrderBy(r => r.CompliancePercentage).ToList(),
                "riskrating" => descending ? rows.OrderByDescending(r => r.RiskRating).ToList() : rows.OrderBy(r => r.RiskRating).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.DocumentOwner).ToList() : rows.OrderBy(r => r.DocumentOwner).ToList()
            };
        }

        private static List<InstitutionDocumentRequestReportRowDto> SortInstitutionRequestRows(
            List<InstitutionDocumentRequestReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "requestDate").Trim().ToLowerInvariant();

            return key switch
            {
                "institution" => descending ? rows.OrderByDescending(r => r.Institution).ToList() : rows.OrderBy(r => r.Institution).ToList(),
                "recipient" => descending ? rows.OrderByDescending(r => r.Recipient).ToList() : rows.OrderBy(r => r.Recipient).ToList(),
                "recipienttype" => descending ? rows.OrderByDescending(r => r.RecipientType).ToList() : rows.OrderBy(r => r.RecipientType).ToList(),
                "submissiondeadline" => descending ? rows.OrderByDescending(r => r.SubmissionDeadline).ToList() : rows.OrderBy(r => r.SubmissionDeadline).ToList(),
                "requestjustification" => descending ? rows.OrderByDescending(r => r.RequestJustification).ToList() : rows.OrderBy(r => r.RequestJustification).ToList(),
                "status" => descending ? rows.OrderByDescending(r => r.Status).ToList() : rows.OrderBy(r => r.Status).ToList(),
                "approvaldenialdate" => descending ? rows.OrderByDescending(r => r.ApprovalDenialDate).ToList() : rows.OrderBy(r => r.ApprovalDenialDate).ToList(),
                "referencenumber" => descending ? rows.OrderByDescending(r => r.ReferenceNumber).ToList() : rows.OrderBy(r => r.ReferenceNumber).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.RequestDate).ToList() : rows.OrderBy(r => r.RequestDate).ToList()
            };
        }

        private static List<DepartmentComplianceReportRowDto> SortDepartmentRows(
            List<DepartmentComplianceReportRowDto> rows,
            string? sortBy,
            string? sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var key = (sortBy ?? "department").Trim().ToLowerInvariant();

            return key switch
            {
                "departmentadmin" => descending ? rows.OrderByDescending(r => r.DepartmentAdmin).ToList() : rows.OrderBy(r => r.DepartmentAdmin).ToList(),
                "requireddocuments" => descending ? rows.OrderByDescending(r => r.RequiredDocuments).ToList() : rows.OrderBy(r => r.RequiredDocuments).ToList(),
                "uploadeddocuments" => descending ? rows.OrderByDescending(r => r.UploadedDocuments).ToList() : rows.OrderBy(r => r.UploadedDocuments).ToList(),
                "missingdocuments" => descending ? rows.OrderByDescending(r => r.MissingDocuments).ToList() : rows.OrderBy(r => r.MissingDocuments).ToList(),
                "compliancepercentage" => descending ? rows.OrderByDescending(r => r.CompliancePercentage).ToList() : rows.OrderBy(r => r.CompliancePercentage).ToList(),
                "riskrating" => descending ? rows.OrderByDescending(r => r.RiskRating).ToList() : rows.OrderBy(r => r.RiskRating).ToList(),
                _ => descending ? rows.OrderByDescending(r => r.Department).ToList() : rows.OrderBy(r => r.Department).ToList()
            };
        }

        private static string ResolveRiskRating(string? riskLevel, int missing, int required)
        {
            if (!string.IsNullOrWhiteSpace(riskLevel))
            {
                return riskLevel;
            }

            if (required == 0)
            {
                return "Unknown";
            }

            var ratio = (decimal)missing / required;
            if (ratio >= 0.5m) return "High";
            if (ratio >= 0.25m) return "Medium";
            return "Low";
        }

        private static string ResolveRiskFromScore(decimal score)
        {
            if (score >= 75m) return "High";
            if (score >= 40m) return "Medium";
            if (score > 0m) return "Low";
            return "Unknown";
        }

        private static string ResolveComplianceStatus(decimal compliancePercentage)
        {
            if (compliancePercentage >= 100m) return "Compliant";
            if (compliancePercentage >= 70m) return "Partial";
            return "Non-Compliant";
        }

        private static bool IsApprovedStatus(string status)
        {
            var normalized = status.Trim().ToLowerInvariant();
            return normalized == "approved" || normalized == "routed_to_owner";
        }

        private static bool IsPendingStatus(string status)
        {
            var normalized = status.Trim().ToLowerInvariant();
            return normalized is "pending" or "department_pending";
        }

        private static bool IsDeniedStatus(string status)
        {
            var normalized = status.Trim().ToLowerInvariant();
            return normalized == "denied";
        }

        private static string BuildFullName(string? firstName, string? lastName)
        {
            var full = $"{firstName} {lastName}".Trim();
            return string.IsNullOrWhiteSpace(full) ? "Unknown" : full;
        }
    }
}
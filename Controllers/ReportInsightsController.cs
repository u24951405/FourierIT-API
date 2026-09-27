using System.Security.Cryptography;
using System.Text;
using FourierIT_API.Data;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    /// <summary>
    /// Reports built entirely from live data: the client risk rating, the institution audit trail,
    /// per-owner compliance certificates, and the list of document owners the per-owner reports pick from.
    /// </summary>
    [ApiController]
    [Route("api/reports")]
    [Authorize(Policy = "Reports.View")]
    public class ReportInsightsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportInsightsController(AppDbContext context)
        {
            _context = context;
        }

        // ── Shared ──────────────────────────────────────────────────────────────

        private sealed record OwnerRow(string UserId, string Name, string? UserName, string EntityType, string? IdentificationNumber, bool IsPep);

        /// <summary>People who upload documents (Document Owners and Department Admins) — the only people compliance applies to.</summary>
        private async Task<List<OwnerRow>> GetDocumentOwnersAsync(string? onlyUserId = null)
        {
            var uploaderRoles = ComplianceService.DocumentUploaderRoles.Select(r => r.ToUpperInvariant()).ToList();
            var uploaderIds = _context.UserRoles
                .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.NormalizedName })
                .Where(x => x.NormalizedName != null && uploaderRoles.Contains(x.NormalizedName))
                .Select(x => x.UserId);

            var query = _context.Users.AsNoTracking().Where(u => uploaderIds.Contains(u.Id));
            if (onlyUserId != null) query = query.Where(u => u.Id == onlyUserId);

            var rows = await query
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    First = u.Profile != null ? u.Profile.FirstName : null,
                    Last = u.Profile != null ? u.Profile.LastName : null,
                    EntityType = u.EntityType != null ? u.EntityType.Name : null,
                    u.EntityIdentificationNumber,
                    u.IsPEPStatus
                })
                .ToListAsync();

            return rows
                .Select(r =>
                {
                    var name = $"{r.First} {r.Last}".Trim();
                    return new OwnerRow(r.Id, string.IsNullOrWhiteSpace(name) ? r.UserName ?? r.Id : name, r.UserName,
                        r.EntityType ?? "Not specified", r.EntityIdentificationNumber, r.IsPEPStatus);
                })
                .OrderBy(r => r.Name)
                .ToList();
        }

        private static string ShortHash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];

        private static string MaskIdentification(string? value)
        {
            var v = value?.Trim() ?? string.Empty;
            if (v.Length == 0) return "Not recorded";
            return v.Length <= 4 ? v : new string('*', v.Length - 4) + v[^4..];
        }

        // ── Owner list (for the per-owner reports) ──────────────────────────────

        [HttpGet("document-owners")]
        public async Task<IActionResult> GetDocumentOwners()
        {
            var owners = await GetDocumentOwnersAsync();
            var statuses = await _context.ComplianceStatuses.AsNoTracking()
                .Where(s => s.UserId != null)
                .Select(s => new { s.UserId, s.OverallStatus })
                .ToListAsync();

            return Ok(owners.Select(o => new
            {
                userId = o.UserId,
                name = o.Name,
                entityType = o.EntityType,
                overallStatus = statuses.FirstOrDefault(s => s.UserId == o.UserId)?.OverallStatus ?? "Not checked yet"
            }));
        }

        // ── Client risk rating ──────────────────────────────────────────────────

        /// <summary>
        /// Every document owner grouped by their risk level. The level comes from real risk factors
        /// (expired, missing or uncertified documents, PEP status, enhanced due diligence), listed per owner.
        /// </summary>
        [HttpGet("client-risk-rating")]
        public async Task<IActionResult> GetClientRiskRating()
        {
            var owners = await GetDocumentOwnersAsync();
            var ownerIds = owners.Select(o => o.UserId).ToList();
            var statuses = await _context.ComplianceStatuses.AsNoTracking()
                .Where(s => s.UserId != null && ownerIds.Contains(s.UserId))
                .ToListAsync();

            var levels = new[] { "Critical", "High", "Medium", "Low", "Not assessed" };
            var profiles = owners.Select(owner =>
            {
                var status = statuses.FirstOrDefault(s => s.UserId == owner.UserId);
                var factors = new List<string>();
                if (status != null)
                {
                    if (status.ExpiredDocuments > 0) factors.Add($"{status.ExpiredDocuments} expired document{(status.ExpiredDocuments == 1 ? "" : "s")}");
                    if (status.MissingDocuments > 0) factors.Add($"{status.MissingDocuments} missing document{(status.MissingDocuments == 1 ? "" : "s")}");
                    if (status.NotCertifiedDocuments > 0) factors.Add($"{status.NotCertifiedDocuments} not certified");
                    if (status.IsPEP || owner.IsPep) factors.Add("Politically exposed person");
                    if (status.RequiresEnhancedDueDiligence) factors.Add("Enhanced due diligence required");
                }

                var level = status == null
                    ? "Not assessed"
                    : levels.FirstOrDefault(l => string.Equals(l, status.RiskLevel, StringComparison.OrdinalIgnoreCase)) ?? "Not assessed";

                return new
                {
                    userId = owner.UserId,
                    name = owner.Name,
                    entityType = owner.EntityType,
                    riskLevel = level,
                    riskScore = status?.OverallRiskScore,
                    compliancePercentage = status?.CompliancePercentage,
                    overallStatus = status?.OverallStatus ?? "Not checked yet",
                    isPep = status?.IsPEP == true || owner.IsPep,
                    factors,
                    lastChecked = status?.LastChecked
                };
            }).ToList();

            var assessed = profiles.Where(p => p.riskScore != null).ToList();
            return Ok(new
            {
                reportId = $"DV-CRR-{DateTime.UtcNow:yyyyMMdd}-{ShortHash(string.Join(",", ownerIds) + DateTime.UtcNow.ToString("yyyyMMddHH"))[..6]}",
                dateGenerated = DateTimeOffset.UtcNow,
                totalProfiles = profiles.Count,
                assessedProfiles = assessed.Count,
                averageRiskScore = assessed.Count == 0 ? 0 : Math.Round(assessed.Average(p => p.riskScore ?? 0), 1),
                pepCount = profiles.Count(p => p.isPep),
                strata = levels
                    .Select(level => new
                    {
                        level,
                        profiles = profiles.Where(p => p.riskLevel == level).OrderByDescending(p => p.riskScore ?? -1).ThenBy(p => p.name).ToList()
                    })
                    .Where(s => s.profiles.Count > 0)
                    .ToList()
            });
        }

        // ── Institution audit trail ─────────────────────────────────────────────

        private static readonly Dictionary<string, string> AuditActionLabels = new()
        {
            ["INSTITUTION_REQUEST_CREATED"] = "Requested documents",
            ["REQUEST_ROUTED_TO_OWNER"] = "Request assigned to an owner",
            ["REQUEST_APPROVED"] = "Request approved",
            ["REQUEST_DENIED"] = "Request denied",
            ["REQUEST_REVOKED"] = "Request cancelled by the institution",
            ["INSTITUTION_DOCUMENT_DOWNLOADED"] = "Downloaded a document",
            ["INSTITUTION_DOCUMENT_VIEWED"] = "Viewed a document in the browser",
            ["ACCESS_EXTENSION_REQUESTED"] = "Asked for more time",
            ["ACCESS_EXTENSION_APPROVED"] = "More time approved",
            ["ACCESS_EXTENSION_DENIED"] = "More time declined",
            ["ACCESS_EXPIRY_REMINDER_SENT"] = "Reminded that access ends soon",
            ["REQUEST_REMINDER_SENT"] = "Owner reminded to answer",
            ["REQUEST_ESCALATED"] = "Overdue request escalated",
            ["DOCUMENT_FLAGGED"] = "Flagged a document",
            ["DOCUMENT_ACCESS_REVOKED"] = "Access to a document withdrawn",
        };

        /// <summary>
        /// What each institution did and what was decided about its requests, from the audit log, for a period
        /// (default: the last 90 days). Flags and withdrawn access are marked for attention.
        /// </summary>
        [HttpGet("system-audit")]
        public async Task<IActionResult> GetSystemAudit([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var periodTo = (to?.Date ?? DateTime.UtcNow.Date).AddDays(1);
            var periodFrom = from?.Date ?? periodTo.AddDays(-91);
            var fromOffset = new DateTimeOffset(DateTime.SpecifyKind(periodFrom, DateTimeKind.Utc));
            var toOffset = new DateTimeOffset(DateTime.SpecifyKind(periodTo, DateTimeKind.Utc));

            var requests = await _context.InstitutionEnquiryRequests.AsNoTracking()
                .Select(r => new { r.EnquiryRequestId, r.InstitutionId, InstitutionName = r.Institution.InstitutionName, r.Status, r.RequestDate, r.PurposeNote })
                .ToListAsync();
            var requestById = requests.ToDictionary(r => r.EnquiryRequestId);

            var approvalToRequest = await _context.DocumentAccessApprovals.AsNoTracking()
                .Select(a => new { a.ApprovalId, a.EnquiryRequestId })
                .ToDictionaryAsync(a => a.ApprovalId, a => a.EnquiryRequestId);

            var codes = AuditActionLabels.Keys.ToList();
            var logs = await _context.AuditLogs.AsNoTracking()
                .Where(l => codes.Contains(l.ActionCode) && l.TimeStamp >= fromOffset && l.TimeStamp < toOffset)
                .OrderBy(l => l.AuditLogId)
                .Select(l => new { l.ActionCode, l.TimeStamp, l.Description, l.TableAffected, l.RecordID, l.InstitutionId })
                .ToListAsync();

            var institutionNames = await _context.Institutions.AsNoTracking()
                .ToDictionaryAsync(i => i.InstitutionId, i => i.InstitutionName);

            // Work out which institution (and request) each entry belongs to.
            var events = logs.Select(l =>
            {
                int? requestId = l.TableAffected switch
                {
                    "InstitutionEnquiryRequests" => l.RecordID,
                    "DocumentAccessApprovals" when l.RecordID != null && approvalToRequest.TryGetValue(l.RecordID.Value, out var rid) => rid,
                    _ => null
                };
                var institutionId = l.InstitutionId ?? (requestId != null && requestById.TryGetValue(requestId.Value, out var req) ? req.InstitutionId : (int?)null);
                return new
                {
                    institutionId,
                    requestId,
                    timestamp = l.TimeStamp,
                    action = AuditActionLabels[l.ActionCode],
                    description = l.Description ?? string.Empty,
                    needsAttention = l.ActionCode is "DOCUMENT_FLAGGED" or "DOCUMENT_ACCESS_REVOKED" or "REQUEST_ESCALATED"
                };
            })
            .Where(e => e.institutionId != null)
            .ToList();

            var requestsInPeriod = requests.Where(r => r.RequestDate >= fromOffset && r.RequestDate < toOffset).ToList();
            var institutionIds = events.Select(e => e.institutionId!.Value)
                .Concat(requestsInPeriod.Select(r => r.InstitutionId))
                .Distinct()
                .ToList();

            var institutions = institutionIds
                .Select(id =>
                {
                    var own = requestsInPeriod.Where(r => r.InstitutionId == id).ToList();
                    var ownEvents = events.Where(e => e.institutionId == id).OrderByDescending(e => e.timestamp).ToList();
                    return new
                    {
                        institutionId = id,
                        institutionName = institutionNames.TryGetValue(id, out var name) ? name : $"Institution #{id}",
                        requests = own.Count,
                        approved = own.Count(r => r.Status == "Approved"),
                        denied = own.Count(r => r.Status == "Denied"),
                        waiting = own.Count(r => r.Status == "Pending" || r.Status == "Department_Pending"),
                        cancelled = own.Count(r => r.Status == "Revoked"),
                        downloads = ownEvents.Count(e => e.action == AuditActionLabels["INSTITUTION_DOCUMENT_DOWNLOADED"]),
                        flags = ownEvents.Count(e => e.action == AuditActionLabels["DOCUMENT_FLAGGED"]),
                        needsAttention = ownEvents.Count(e => e.needsAttention),
                        events = ownEvents.Select(e => new { e.timestamp, e.action, e.description, e.requestId, e.needsAttention })
                    };
                })
                .OrderByDescending(i => i.events.Count())
                .ThenBy(i => i.institutionName)
                .ToList();

            return Ok(new
            {
                reportId = $"DV-SAR-{DateTime.UtcNow:yyyyMMdd}-{ShortHash($"{periodFrom:yyyyMMdd}{periodTo:yyyyMMdd}{events.Count}")[..6]}",
                dateGenerated = DateTimeOffset.UtcNow,
                periodFrom,
                periodTo = periodTo.AddDays(-1),
                totalEvents = events.Count,
                totalRequests = requestsInPeriod.Count,
                totalDownloads = institutions.Sum(i => i.downloads),
                totalNeedingAttention = institutions.Sum(i => i.needsAttention),
                institutions
            });
        }

        // ── Compliance certificate ──────────────────────────────────────────────

        /// <summary>
        /// A compliance statement for one document owner, from their latest compliance check. It is only a
        /// certificate of compliance when they are fully compliant; otherwise it lists what is outstanding.
        /// </summary>
        [HttpGet("compliance-certificate/{userId}")]
        public async Task<IActionResult> GetComplianceCertificate([FromRoute] string userId)
        {
            var owner = (await GetDocumentOwnersAsync(userId)).FirstOrDefault();
            if (owner == null)
                return NotFound(new { error = "No document owner was found for this certificate." });

            var status = await _context.ComplianceStatuses.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            var documents = await _context.Documents.AsNoTracking()
                .Where(d => d.UserId == userId && d.CurrentStatus != "Deleted")
                .OrderBy(d => d.DocumentType.TypeName)
                .Select(d => new { typeName = d.DocumentType.TypeName, fileName = d.FileName, status = d.CurrentStatus, expiryDate = d.ExpiryDate })
                .ToListAsync();

            var isCompliant = status != null
                && string.Equals(status.OverallStatus, "Compliant", StringComparison.OrdinalIgnoreCase)
                && status.CompliancePercentage >= 100;

            var issues = new List<string>();
            if (status == null) issues.Add("No compliance check has been run for this owner yet.");
            else
            {
                if (status.MissingDocuments > 0) issues.Add($"{status.MissingDocuments} required document{(status.MissingDocuments == 1 ? " is" : "s are")} missing.");
                if (status.ExpiredDocuments > 0) issues.Add($"{status.ExpiredDocuments} document{(status.ExpiredDocuments == 1 ? " has" : "s have")} expired.");
                if (status.NotCertifiedDocuments > 0) issues.Add($"{status.NotCertifiedDocuments} document{(status.NotCertifiedDocuments == 1 ? " is" : "s are")} not certified.");
                if (status.PendingReviewDocuments > 0) issues.Add($"{status.PendingReviewDocuments} document{(status.PendingReviewDocuments == 1 ? " is" : "s are")} waiting for a Compliance Officer's review.");
                if (status.NonCompliantDocuments > 0 && issues.Count == 0) issues.Add($"{status.NonCompliantDocuments} document{(status.NonCompliantDocuments == 1 ? " does" : "s do")} not meet the requirements.");
                if (!isCompliant && issues.Count == 0) issues.Add($"The latest compliance check rated this owner {status.OverallStatus}.");
            }

            // A certificate is only as current as the earliest-expiring document behind it.
            DateTimeOffset? validUntil = isCompliant && documents.Count > 0 ? documents.Min(d => d.expiryDate) : null;
            var generated = DateTimeOffset.UtcNow;
            var hashInput = $"{userId}|{status?.OverallStatus}|{status?.CompliancePercentage}|{status?.LastChecked:O}|{generated:yyyyMMdd}";

            return Ok(new
            {
                certificateId = $"DV-CERT-{generated:yyyyMMdd}-{ShortHash(hashInput)[..6]}",
                dateGenerated = generated,
                ownerName = owner.Name,
                entityType = owner.EntityType,
                identification = MaskIdentification(owner.IdentificationNumber),
                isCompliant,
                overallStatus = status?.OverallStatus ?? "Not checked yet",
                compliancePercentage = status?.CompliancePercentage ?? 0,
                riskLevel = status?.RiskLevel ?? "Not assessed",
                lastChecked = status?.LastChecked,
                validUntil,
                issues,
                documents,
                verificationHash = ShortHash(hashInput)
            });
        }
    }
}

using FourierIT_API.Data;
using FourierIT_API.DTOs.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
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
                .Select(d => new { d.UserId, d.DocumentTypeId, d.UploadedDate })
                .ToListAsync();

            var userUploadDocTypes = documents
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

                var compliancePct = totalRequired == 0
                    ? 100m
                    : Math.Round((decimal)matchedUploaded * 100m / totalRequired, 2);

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
                .Where(ur => ur.Role.NormalizedName == "DEPARTMENT ADMIN" && ur.User.DepartmentId.HasValue && departmentIds.Contains(ur.User.DepartmentId.Value))
                .Select(ur => new
                {
                    DepartmentId = ur.User.DepartmentId!.Value,
                    FirstName = ur.User.Profile.FirstName,
                    LastName = ur.User.Profile.LastName,
                    ur.User.Email
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
            var query = _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.TargetUser)
                .Include(r => r.TargetDepartment)
                .Include(r => r.AccessToken)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(r => r.RequestDate >= startDate.Value || r.RespondedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.RequestDate <= endDate.Value || r.RespondedAt <= endDate.Value);
            }

            if (institutionId.HasValue)
            {
                query = query.Where(r => r.InstitutionId == institutionId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToLowerInvariant();
                query = query.Where(r => r.Status.ToLower() == normalized);
            }

            var requestRows = await query
                .Select(r => new InstitutionAccessHistoryReportRowDto
                {
                    RequestId = r.EnquiryRequestId,
                    Institution = r.Institution.InstitutionName,
                    Recipient = r.RequestType == "Department"
                        ? (r.TargetDepartment != null ? r.TargetDepartment.DepartmentName : "Unknown Department")
                        : ((r.TargetUser != null ? r.TargetUser.Profile.FirstName : "") + " " + (r.TargetUser != null ? r.TargetUser.Profile.LastName : "")).Trim(),
                    RecipientType = r.RequestType,
                    AccessGrantedDate = r.RespondedAt,
                    AccessExpiry = r.AccessToken.ExpiryTimeStamp,
                    AccessStatus = r.Status,
                    DocumentsAccessed = Array.Empty<string>()
                })
                .ToListAsync();

            var requestIds = requestRows.Select(r => r.RequestId).ToList();
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
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).Distinct().OrderBy(n => n).ToArray());

            foreach (var row in requestRows)
            {
                row.DocumentsAccessed = docsByRequest.TryGetValue(row.RequestId, out var docs)
                    ? docs
                    : Array.Empty<string>();
            }

            return requestRows;
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
                .Where(d => d.CurrentStatus != "Deleted");

            if (departmentId.HasValue)
            {
                query = query.Where(d => d.User.DepartmentId == departmentId.Value);
            }

            if (institutionId.HasValue)
            {
                query = query.Where(d => d.User.Department != null && d.User.Department.Branch.InstitutionId == institutionId.Value);
            }

            var rows = await query
                .Select(d => new ExpiringDocumentsReportRowDto
                {
                    Owner = d.User.Profile != null
                        ? (d.User.Profile.FirstName + " " + d.User.Profile.LastName).Trim()
                        : (d.User.Email ?? d.User.UserName ?? d.User.Id),
                    Department = d.User.Department != null ? d.User.Department.DepartmentName : "Unknown",
                    DocumentType = d.DocumentType.TypeName,
                    ExpiryDate = d.ExpiryDate,
                    DaysRemaining = (int)((d.ExpiryDate.UtcDateTime - DateTime.UtcNow).TotalDays)
                })
                .ToListAsync();

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
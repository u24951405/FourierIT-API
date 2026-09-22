using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using FourierIT_API.Services;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IDocumentRepository _documentRepository;
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _context;
        private readonly IComplianceService _complianceService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<DocumentController> _logger;
        private readonly DocumentValidityCalculator _documentValidityCalculator;

        public DocumentController(IDocumentService documentService, IDocumentRepository documentRepository, UserManager<User> userManager, AppDbContext context, IComplianceService complianceService, IAuditLogService auditLogService, ILogger<DocumentController> logger, DocumentValidityCalculator? documentValidityCalculator = null)
        {
            _documentService = documentService;
            _documentRepository = documentRepository;
            _userManager = userManager;
            _context = context;
            _complianceService = complianceService;
            _auditLogService = auditLogService;
            _logger = logger;
            _documentValidityCalculator = documentValidityCalculator ?? new DocumentValidityCalculator();
        }

        //Role helper method
        private async Task<bool> IsAdminOrViewerRole(User user)
        {
            // The seeded Super Admin account has no roles at all - it is granted access via a
            // "superadmin" claim that bypasses [Authorize] attributes, but that bypass doesn't
            // reach this hand-rolled ownership check, so it must be tested for explicitly here.
            if (User.HasClaim("superadmin", "true"))
                return true;

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Department Admin") || roles.Contains("Compliance Officer") || roles.Contains("Stakeholder");
        }

        private async Task<bool> UserCanAccessDepartmentAsync(int departmentId)
        {
            if (User.IsInRole("Admin"))
                return true;

            if (!User.IsInRole("Department Admin") && !User.IsInRole("Stakeholder"))
                return false;

            var currentUser = await _userManager.GetUserAsync(User);
            return currentUser != null && currentUser.DepartmentId == departmentId;
        }

        [HttpGet("api/users/me/documents/required")]
        public async Task<IActionResult> GetRequiredDocumentsStatus([FromQuery] int? entityTypeId = null) //? makes the parameter optional
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            bool isDepartmentAdmin = await _userManager.IsInRoleAsync(user, "Department Admin");
            var documentQuery = _context.Documents.Where(d => d.UserId == user.Id && d.CurrentStatus != "Deleted");

            var activeRequestedDocuments = await GetActiveRequestedDocumentTypesAsync(user);
            if (activeRequestedDocuments.Any())
            {
                var requestedDocumentTypeIds = activeRequestedDocuments.Select(rd => rd.DocumentTypeId).Distinct().ToList();
                var requestedUserDocs = await documentQuery
                    .Where(d => requestedDocumentTypeIds.Contains(d.DocumentTypeId))
                    .ToListAsync();

                var requestedResult = activeRequestedDocuments
                    .OrderBy(rd => rd.DocumentTypeName)
                    .ThenBy(rd => rd.IsMandatory ? 0 : 1)
                    .Select(rd =>
                    {
                        var docs = requestedUserDocs.Where(d => d.DocumentTypeId == rd.DocumentTypeId).ToList();
                        var hasNonRejected = docs.Any(d => !string.Equals(d.CurrentStatus, "Rejected", StringComparison.OrdinalIgnoreCase));
                        var hasRejectedOnly = docs.Any() && !hasNonRejected;
                        var isUploaded = hasNonRejected;

                        return new
                        {
                            rd.DocumentTypeId,
                            DocumentTypeName = rd.DocumentTypeName,
                            rd.IsMandatory,
                            Description = rd.DocumentTypeDescription,
                            IsUploaded = isUploaded,
                            UploadCount = docs.Count
                        };
                    })
                    .ToList();

                var missingCount = requestedResult.Count(r => !r.IsUploaded);
                var isComplete = requestedResult.All(r => r.IsUploaded);

                return Ok(new
                {
                    EntityType = "Requested Documents",
                    IsComplete = isComplete,
                    MissingCount = missingCount,
                    Documents = requestedResult
                });
            }

            if (isDepartmentAdmin && user.DepartmentId.HasValue)
            {
                var department = await _context.Departments
                    .Include(d => d.DepartmentDocumentTypes)
                    .ThenInclude(ddt => ddt.DocumentType)
                    .FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId.Value);

                if (department == null)
                    return NotFound(new { error = "Assigned department not found." });

                var departmentRequiredDocs = department.DepartmentDocumentTypes
                    .OrderBy(ddt => ddt.IsMandatory ? 0 : 1)
                    .ThenBy(ddt => ddt.DocumentType.TypeName)
                    .ToList();

                var departmentUserDocs = await documentQuery
                    .GroupBy(d => d.DocumentTypeId)
                    .Select(g => new
                    {
                        DocumentTypeId = g.Key,
                        count = g.Count()
                    })
                    .ToDictionaryAsync(x => x.DocumentTypeId, x => x.count);

                var departmentResult = departmentRequiredDocs.Select(rd => new
                {
                    rd.DocumentTypeId,
                    DocumentTypeName = rd.DocumentType.TypeName,
                    rd.IsMandatory,
                    rd.DocumentType.Description,
                    IsUploaded = departmentUserDocs.ContainsKey(rd.DocumentTypeId),
                    UploadCount = departmentUserDocs.GetValueOrDefault(rd.DocumentTypeId, 0)
                });

                var departmentMissingMandatory = departmentResult.Where(r => r.IsMandatory && !r.IsUploaded).ToList();

                return Ok(new
                {
                    EntityType = department.DepartmentName,
                    IsComplete = !departmentMissingMandatory.Any(),
                    MissingCount = departmentMissingMandatory.Count,
                    Documents = departmentResult
                });
            }

            // 1. Get user's default entity type
            var userWithEntity = await _context.Users
                .Include(u => u.EntityType)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            // 2. Determine which EntityTypeId to use
            int targetEntityTypeId;

            if (entityTypeId.HasValue)
            {
                targetEntityTypeId = entityTypeId.Value;
            }
            else if (userWithEntity?.EntityTypeId != null)
            {
                targetEntityTypeId = userWithEntity.EntityTypeId.Value;
            }
            else
            {
                return BadRequest(new { error = "User has not selected an entity type. Please specify an entity type" });
            }

            // 3. Get required documents for the determined entity type
            var entityType = await _context.EntityTypes
                .Include(et => et.RequiredDocuments)
                .ThenInclude(rd => rd.DocumentType)
                .FirstOrDefaultAsync(et => et.EntityTypeId == targetEntityTypeId);

            if (entityType == null)
                return NotFound(new { error = "Invalid entity type. " });

            var requiredDocs = entityType.RequiredDocuments;

            var userDocs = await documentQuery
                .GroupBy(d => d.DocumentTypeId)
                .Select(g => new
                {
                    DocumentTypeId = g.Key,
                    count = g.Count()
                })
                .ToDictionaryAsync(x => x.DocumentTypeId, x => x.count);

            var result = requiredDocs.Select(rd => new
            {
                rd.DocumentTypeId,
                DocumentTypeName = rd.DocumentType.TypeName,
                rd.IsMandatory,
                rd.Description,
                IsUploaded = userDocs.ContainsKey(rd.DocumentTypeId),
                UploadCount = userDocs.GetValueOrDefault(rd.DocumentTypeId, 0)
            });

            var missingMandatory = result.Where(r => r.IsMandatory && !r.IsUploaded).ToList();

            return Ok(new
            {
                EntityType = entityType.Name,
                IsComplete = !missingMandatory.Any(),
                MissingCount = missingMandatory.Count,
                Documents = result
            });
        }

        private async Task<List<(int DocumentTypeId, string DocumentTypeName, bool IsMandatory, string? DocumentTypeDescription)>> GetActiveRequestedDocumentTypesAsync(User user)
        {
            var userRequestTypes = _context.InstitutionRequestedDocumentTypes
                .Include(irdt => irdt.DocumentType)
                .Where(irdt => irdt.InstitutionEnquiryRequest.Status == "Pending"
                    && irdt.InstitutionEnquiryRequest.TargetUserId == user.Id);

            var departmentRequestTypes = user.DepartmentId.HasValue
                ? _context.InstitutionRequestedDocumentTypes
                    .Include(irdt => irdt.DocumentType)
                    .Where(irdt => irdt.InstitutionEnquiryRequest.Status == "Department_Pending"
                        && irdt.InstitutionEnquiryRequest.TargetDepartmentId == user.DepartmentId.Value)
                : _context.InstitutionRequestedDocumentTypes
                    .Include(irdt => irdt.DocumentType)
                    .Where(irdt => false);

            var requestedTypes = await userRequestTypes.Concat(departmentRequestTypes).ToListAsync();

            return requestedTypes
                .GroupBy(irdt => irdt.DocumentTypeId)
                .Select(g => (
                    DocumentTypeId: g.Key,
                    DocumentTypeName: g.First().DocumentType.TypeName,
                    IsMandatory: g.Any(irdt => irdt.isMandatory),
                    DocumentTypeDescription: g.First().DocumentType.Description
                ))
                .ToList();
        }

        private async Task<List<int>> GetActiveRequestedDocumentTypeIdsAsync(User user)
        {
            var activeRequestedDocumentTypes = await GetActiveRequestedDocumentTypesAsync(user);
            return activeRequestedDocumentTypes.Select(r => r.DocumentTypeId).Distinct().ToList();
        }

        private async Task<bool> HasDocumentManagePermissionAsync(string userId)
        {
            return await _context.Set<IdentityUserRole<string>>()
                .Where(userRole => userRole.UserId == userId)
                .Join(
                    _context.RolePermissions,
                    userRole => userRole.RoleId,
                    rolePermission => rolePermission.RoleId,
                    (_, rolePermission) => rolePermission.PermissionId)
                .Join(
                    _context.Permissions,
                    permissionId => permissionId,
                    permission => permission.PermissionId,
                    (_, permission) => permission.PermissionKey)
                .AnyAsync(permissionKey => permissionKey == "Documents.Manage");
        }

        private async Task<bool> CanManageDocumentAsync(User user, Document document)
        {
            if (User.HasClaim("superadmin", "true"))
                return true;

            return document.UserId == user.Id || await HasDocumentManagePermissionAsync(user.Id);
        }

        private async Task<(bool IsValid, string? ErrorMessage)> ValidateDocumentTypeForUserContextAsync(User user, int documentTypeId, int? entityTypeId)
        {
            var activeRequestDocumentTypeIds = await GetActiveRequestedDocumentTypeIdsAsync(user);
            if (activeRequestDocumentTypeIds.Any())
            {
                if (!activeRequestDocumentTypeIds.Contains(documentTypeId))
                {
                    return (false, "The specified document type is not requested by an active institution request.");
                }
                return (true, null);
            }

            if (!entityTypeId.HasValue)
            {
                return (false, "Please select an entity type first.");
            }

            var entityType = await _context.EntityTypes
                .Include(et => et.RequiredDocuments)
                .FirstOrDefaultAsync(et => et.EntityTypeId == entityTypeId.Value);
            if (entityType == null)
            {
                return (false, "Invalid entity type selected.");
            }

            var allowedForEntity = entityType.RequiredDocuments.Any(rd => rd.DocumentTypeId == documentTypeId);
            if (!allowedForEntity)
            {
                return (false, "The specified document type is not required/allowed for the selected entity type.");
            }

            return (true, null);
        }

        [HttpPost("upload")]
        [Authorize(Policy = "Documents.Upload")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var activeRequestDocumentTypeIds = await GetActiveRequestedDocumentTypeIdsAsync(user);
            var hasActiveRequestContext = activeRequestDocumentTypeIds.Any();

            EntityType? entityType = null;
            int? effectiveEntityTypeId = dto.EntityTypeId ?? user.EntityTypeId;

            if (effectiveEntityTypeId.HasValue)
            {
                entityType = await _context.EntityTypes
                    .Include(et => et.RequiredDocuments)
                    .FirstOrDefaultAsync(et => et.EntityTypeId == effectiveEntityTypeId.Value);

                if (entityType == null)
                {
                    return BadRequest(new { error = "Invalid entity type selected." });
                }
            }
            else if (!hasActiveRequestContext)
            {
                return BadRequest(new { error = "Please select an entity type first." });
            }

            // Prevent duplicate uploads of the same document type for the same user
            var existingUpload = await _context.Documents
                .AnyAsync(d => d.UserId == user.Id
                    && d.DocumentTypeId == dto.DocumentTypeId
                    && d.CurrentStatus != "Deleted");

            if (existingUpload)
            {
                return BadRequest(new { error = "You have already uploaded this document type. Please choose a different type or update the existing document." });
            }
            var validationResult = await ValidateDocumentTypeForUserContextAsync(user, dto.DocumentTypeId, effectiveEntityTypeId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            // If user had no saved entity (registered with both roles) and supplied one now, persist it to their account
            if (user.EntityTypeId == null && dto.EntityTypeId.HasValue)
            {
                user.EntityTypeId = dto.EntityTypeId.Value;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, updateResult.Errors);
                }
            }

            using var ms = new MemoryStream();
            await dto.File.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            Document document;
            try
            {
                document = await _documentService.UploadDocumentAsync(user.Id, dto.File.FileName, fileBytes, dto.DocumentTypeId);
                document.IsCertified = dto.IsCertified;

                if (dto.IsCertified && !string.IsNullOrEmpty(dto.CommissionerName) && dto.CertificationDate.HasValue)
                {
                    document.CertificationDetails.Add(new CertificationDetails
                    {
                        CertificationID = Guid.NewGuid().ToString(),
                        CommissionerName = dto.CommissionerName,
                        CertificationDate = dto.CertificationDate.Value,
                        DocumentId = document.DocumentId
                    });
                }

                await _documentRepository.UpdateDocumentAsync(document);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            var documentTypePolicy = await _context.DocumentTypes.FindAsync(dto.DocumentTypeId);
            if (documentTypePolicy == null)
                return BadRequest(new { error = "The specified document type could not be found." });

            var validityResult = _documentValidityCalculator.Calculate(
                documentTypePolicy,
                document.UploadedDate,
                dto.CertificationDate.HasValue ? new DateTimeOffset(dto.CertificationDate.Value) : null);
            document.ExpiryDate = validityResult.ExpiryDate;

            await _documentRepository.UpdateDocumentAsync(document);

            try
            {
                await _complianceService.CheckUserComplianceAsync(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate compliance after document upload for user {UserId}", user.Id);
            }
            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, ToResponseDto(document));
        }

        [HttpGet]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> GetMyDocuments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "Super Admin");
            if (isSuperAdmin)
            {
                var superAdminDocs = await _context.Documents
                    .Include(d => d.DocumentType)
                    .Where(d => d.CurrentStatus != "Deleted")
                    .OrderByDescending(d => d.UploadedDate)
                    .ToListAsync();

                return Ok(superAdminDocs.Select(ToResponseDto));
            }

            var userDocs = await _documentRepository.GetUserDocumentAsync(user.Id);
            return Ok(userDocs.Select(ToResponseDto));
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();


            // Allow if: owner or admin/viewer role
            var isAdminViewer = await IsAdminOrViewerRole(user);
            if (doc.UserId != user.Id && !isAdminViewer)
                return Forbid();

            return Ok(ToResponseDto(doc));
        }

        [HttpGet("{id}/access")]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> GetDocumentAccess(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var document = await _documentRepository.GetDocumentByIdAsync(id);
            if (document == null) return NotFound();
            if (document.UserId != user.Id) return Forbid();

            var accessApprovals = await _context.DocumentAccessApprovals
                .Include(daa => daa.InstitutionEnquiryRequest)
                    .ThenInclude(ier => ier.Institution)
                .Include(daa => daa.ApprovedByUser)
                .Where(daa => daa.DocumentId == id && !daa.IsRevoked)
                .ToListAsync();

            var response = accessApprovals.Select(daa => new DocumentAccessApprovalDto
            {
                ApprovalId = daa.ApprovalId,
                InstitutionId = daa.InstitutionEnquiryRequest.InstitutionId,
                InstitutionName = daa.InstitutionEnquiryRequest.Institution.InstitutionName,
                ApprovedByUserName = daa.ApprovedByUser?.UserName ?? string.Empty,
                ApprovedAt = daa.ApprovedAt
            });

            return Ok(response);
        }

        [HttpGet("{id}/preview")]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> Preview(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var doc = await _documentRepository.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound();

                var isAdminViewer = await IsAdminOrViewerRole(user);
                if (doc.UserId != user.Id && !isAdminViewer)
                    throw new UnauthorizedAccessException("No access to this document");

                var fileBytes = await _documentService.DownloadDocumentAsync(id, doc.UserId);

                doc.LastAccessedDate = DateTime.UtcNow;
                await _documentRepository.UpdateDocumentAsync(doc);

                _context.DocumentAccessLogs.Add(new DocumentAccessLog
                {
                    DocumentId = id,
                    AccessedByUserId = user.Id,
                    ActionType = isAdminViewer ? "AdminPreview" : "Preview",
                    AccessDateTime = DateTime.UtcNow,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    UserAgent = Request.Headers.UserAgent.ToString()
                });
                await _context.SaveChangesAsync();

                return File(fileBytes, GetMimeType(doc.FileName));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpGet("{id}/download")]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> Download(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var doc = await _documentRepository.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound();

                //check access: owner or admin/viewer
                var isAdminViewer = await IsAdminOrViewerRole(user);
                if (doc.UserId != user.Id && !isAdminViewer)
                    throw new UnauthorizedAccessException("No access to this document");

                var fileBytes = await _documentService.DownloadDocumentAsync(id, doc.UserId); // decrypt using owner's key context

                doc.LastAccessedDate = DateTime.UtcNow;
                await _documentRepository.UpdateDocumentAsync(doc);

                _context.DocumentAccessLogs.Add(new DocumentAccessLog
                {
                    DocumentId = id,
                    AccessedByUserId = user.Id,
                    ActionType = isAdminViewer ? "AdminDownload" : "Download",
                    AccessDateTime = DateTime.UtcNow,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    UserAgent = Request.Headers.UserAgent.ToString()
                });
                await _context.SaveChangesAsync();

                Response.Headers.ContentDisposition = $"attachment; filename=\"{doc.FileName}\"";
                return File(fileBytes, GetMimeType(doc.FileName));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpDelete("{id}/access/{approvalId}")]
        [Authorize]
        public async Task<IActionResult> RevokeDocumentAccess(int id, int approvalId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();

            // The document owner, or someone with Documents.Manage permission, can revoke access
            if (!await CanManageDocumentAsync(user, doc)) return Forbid();

            var approval = await _context.DocumentAccessApprovals
                .FirstOrDefaultAsync(daa => daa.ApprovalId == approvalId && daa.DocumentId == id);

            if (approval == null) return NotFound(new { error = "Access approval not found." });

            approval.IsRevoked = true;
            approval.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = user.Id,
                ActionCode = "DOCUMENT_ACCESS_REVOKED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Document {id} access approval {approvalId} revoked by owner.",
                TableAffected = "DocumentAccessApprovals",
                RecordID = approvalId
            });

            return NoContent();
        }

        [HttpGet("flags")]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> GetMyDocumentFlags()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var myDocuments = await _context.Documents
                .Where(d => d.UserId == user.Id && d.CurrentStatus != "Deleted")
                .Select(d => new { d.DocumentId, d.FileName })
                .ToListAsync();
            var myDocumentIds = myDocuments.Select(d => d.DocumentId).ToHashSet();

            var flags = await _context.EnquiryFlags
                .Where(f => myDocumentIds.Contains(f.DocumentId))
                .OrderByDescending(f => f.FlaggedAt)
                .ToListAsync();

            var enquiryIds = flags.Select(f => f.EnquiryId).Distinct().ToList();
            var institutionNames = await _context.InstitutionEnquiryRequests
                .Where(r => enquiryIds.Contains(r.EnquiryRequestId))
                .Include(r => r.Institution)
                .ToDictionaryAsync(r => r.EnquiryRequestId, r => r.Institution.InstitutionName);
            var fileNames = myDocuments.ToDictionary(d => d.DocumentId, d => d.FileName);

            var response = flags.Select(f => new DocumentFlagDto
            {
                EnquiryFlagId = f.EnquiryFlagId,
                DocumentId = f.DocumentId,
                FileName = fileNames.GetValueOrDefault(f.DocumentId, string.Empty),
                InstitutionName = institutionNames.GetValueOrDefault(f.EnquiryId, "Institution"),
                FlagReason = f.FlagReason,
                IsResolved = f.IsResolved,
                FlaggedAt = f.FlaggedAt
            });

            return Ok(response);
        }

        [HttpPost("{id}/flags/{flagId}/resolve")]
        [Authorize]
        public async Task<IActionResult> ResolveDocumentFlag(int id, int flagId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();

            // The document owner, or someone with Documents.Manage permission, can resolve a flag
            if (!await CanManageDocumentAsync(user, doc)) return Forbid();

            var flag = await _context.EnquiryFlags
                .FirstOrDefaultAsync(f => f.EnquiryFlagId == flagId && f.DocumentId == id);

            if (flag == null) return NotFound(new { error = "Flag not found." });

            flag.IsResolved = true;
            await _context.SaveChangesAsync();

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = user.Id,
                ActionCode = "DOCUMENT_FLAG_RESOLVED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Document {id} flag {flagId} marked resolved by owner.",
                TableAffected = "EnquiryFlags",
                RecordID = flagId
            });

            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromForm] UploadDocumentDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            if (!await CanManageDocumentAsync(user, doc)) return Forbid();

            if (dto.File != null && dto.File.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.File.CopyToAsync(ms);
                var fileBytes = ms.ToArray();

                Document updated;
                try
                {
                    updated = await _documentService.UploadDocumentAsync(user.Id, dto.File.FileName, fileBytes, dto.DocumentTypeId);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new { error = ex.Message });
                }

                doc.DocumentBlob = updated.DocumentBlob;
                doc.FileName = dto.File.FileName;
                doc.FileSizeBytes = fileBytes.Length;
            }

            doc.DocumentTypeId = dto.DocumentTypeId;
            doc.IsCertified = dto.IsCertified;
            doc.LastModifiedDate = DateTime.UtcNow;

            var updateValidation = await ValidateDocumentTypeForUserContextAsync(user, dto.DocumentTypeId, dto.EntityTypeId ?? user.EntityTypeId);
            if (!updateValidation.IsValid)
            {
                return BadRequest(new { error = updateValidation.ErrorMessage });
            }

            await _documentRepository.UpdateDocumentAsync(doc);
            try
            {
                await _complianceService.CheckUserComplianceAsync(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate compliance after document update for user {UserId}", user.Id);
            }

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = user.Id,
                ActionCode = "DOCUMENT_UPDATED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Document {doc.DocumentId} updated by owner.",
                TableAffected = "Documents",
                RecordID = doc.DocumentId
            });

            return Ok(ToResponseDto(doc));
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            if (!await CanManageDocumentAsync(user, doc)) return Forbid();

            var accessApprovals = await _context.DocumentAccessApprovals
                .Include(daa => daa.InstitutionEnquiryRequest)
                    .ThenInclude(ier => ier.Institution)
                .Include(daa => daa.ApprovedByUser)
                .Where(daa => daa.DocumentId == id && !daa.IsRevoked)
                .ToListAsync();

            if (accessApprovals.Any())
            {
                return Conflict(new
                {
                    message = "Document cannot be deleted while access approvals exist.",
                    approvals = accessApprovals.Select(daa => new
                    {
                        approvalId = daa.ApprovalId,
                        institutionId = daa.InstitutionEnquiryRequest.InstitutionId,
                        institutionName = daa.InstitutionEnquiryRequest.Institution.InstitutionName,
                        approvedByUserName = daa.ApprovedByUser?.UserName ?? string.Empty,
                        approvedAt = daa.ApprovedAt
                    })
                });
            }

            await _documentRepository.DeleteDocumentAsync(id);

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = user.Id,
                ActionCode = "DOCUMENT_DELETED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Document {doc.DocumentId} deleted by owner.",
                TableAffected = "Documents",
                RecordID = doc.DocumentId
            });

            try
            {
                await _complianceService.CheckUserComplianceAsync(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate compliance after document deletion for user {UserId}", user.Id);
            }
            return NoContent();
        }

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".tif" or ".tiff" => "image/tiff",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/octet-stream"
            };
        }

        [HttpGet("departments/{departmentId}")]
        [Authorize(Roles = "Admin,Department Admin,Stakeholder")]
        public async Task<IActionResult> GetDepartmentDocuments(int departmentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (!await UserCanAccessDepartmentAsync(departmentId))
                return Forbid();

            var documents = await _context.Documents
                .AsNoTracking()
                .Include(d => d.DocumentType)
                .Where(d => d.User.DepartmentId == departmentId && d.CurrentStatus != "Deleted")
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();

            return Ok(documents.Select(ToResponseDto));
        }

        //Admin/compliance/stakeholder endpoint to view all users and their documents
        [HttpGet("admin/all-users-documents")]
        [Authorize(Policy = "Documents.View")]
        public async Task<IActionResult> GetAllUsersWithDocuments()
        {
            var users = await _context.Users
                .Include(u => u.Documents)
                .ThenInclude(d => d.DocumentType)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.UserName,
                    EntityType = u.EntityType != null ? u.EntityType.Name : "Not Selected",
                    DocumentCount = u.Documents.Count(d => d.CurrentStatus != "Deleted"),
                    Documents = u.Documents
                    .Where(d => d.CurrentStatus != "Deleted")
                    .Select(d => new
                    {
                        d.DocumentId,
                        d.FileName,
                        d.CurrentStatus,
                        d.IsCertified,
                        d.UploadedDate,
                        DocumentTypeName = d.DocumentType.TypeName
                    })

                }).ToListAsync();

            return Ok(users);
        }

        private static DocumentResponseDto ToResponseDto(Document doc) => new()
        {
            DocumentId = doc.DocumentId,
            FileName = doc.FileName,
            CurrentStatus = doc.CurrentStatus,
            IsCertified = doc.IsCertified,
            IsEncrypted = doc.IsEncrypted,
            EncryptionAlgorithm = doc.EncryptionAlgorithm,
            FileSizeBytes = doc.FileSizeBytes,
            UploadedDate = doc.UploadedDate,
            ExpiryDate = doc.ExpiryDate,
            LastModifiedDate = doc.LastModifiedDate,
            DocumentTypeId = doc.DocumentTypeId,
            DocumentTypeName = doc.DocumentType?.TypeName ?? string.Empty
        };

        private async Task TryCreateAuditLogAsync(AuditLog auditLog)
        {
            if (auditLog == null) return;

            try
            {
                await _auditLogService.CreateAuditLogAsync(auditLog);
            }
            catch
            {
                // Do not fail the user action if audit logging is unavailable.
            }
        }

        [HttpGet("api/users/me/document-types")]
        public async Task<IActionResult> GetDocumentTypesForMyEntity()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isDepartmentAdmin = await _userManager.IsInRoleAsync(user, "Department Admin");
            if (isDepartmentAdmin && user.DepartmentId.HasValue)
            {
                var department = await _context.Departments
                    .Include(d => d.DepartmentDocumentTypes)
                    .ThenInclude(ddt => ddt.DocumentType)
                    .FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId.Value);

                if (department == null)
                    return NotFound(new { error = "Assigned department not found." });

                var departmentDocumentTypes = department.DepartmentDocumentTypes
                    .OrderBy(ddt => ddt.IsMandatory ? 0 : 1)
                    .ThenBy(ddt => ddt.DocumentType.TypeName)
                    .Select(ddt => new
                    {
                        ddt.DocumentTypeId,
                        ddt.DocumentType.TypeName,
                        ddt.DocumentType.Description,
                        ddt.IsMandatory,
                        RequirementNote = ddt.DocumentType.Description
                    })
                    .ToList();

                return Ok(new
                {
                    EntityType = department.DepartmentName,
                    DocumentCount = departmentDocumentTypes.Count,
                    MandatoryCount = departmentDocumentTypes.Count(r => r.IsMandatory),
                    DocumentTypes = departmentDocumentTypes
                });
            }

            // Get user with their entity type
            var userWithEntity = await _context.Users
                .Include(u => u.EntityType)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (userWithEntity?.EntityType == null)
                return BadRequest(new { error = "Please select an entity type first " });

            // Get required documents for this entity type
            var requiredDocs = await _context.RequiredDocuments
                .Where(rd => rd.EntityTypeId == userWithEntity.EntityTypeId)
                .Include(rd => rd.DocumentType)
                .OrderBy(rd => rd.IsMandatory ? 0 : 1) // Mandatory first
                .ThenBy(rd => rd.DocumentType.TypeName)
                .Select(rd => new
                {
                    rd.DocumentTypeId,
                    rd.DocumentType.TypeName,
                    rd.DocumentType.Description,
                    rd.IsMandatory,
                    RequirementNote = rd.Description
                })
                .ToListAsync();

            return Ok(new
            {
                EntityType = userWithEntity.EntityType.Name,
                DocumentCount = requiredDocs.Count,
                MandatoryCount = requiredDocs.Count(r => r.IsMandatory),
                DocumentTypes = requiredDocs
            });
        }
    }

}

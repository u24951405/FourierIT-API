using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class DocumentAccessRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IDocumentService _documentService;
        private readonly DepartmentRequestValidationService _departmentRequestValidationService;
        private readonly IAuditLogService _auditLogService;

        public DocumentAccessRequestsController(
            AppDbContext context,
            UserManager<User> userManager,
            IDocumentService documentService,
            DepartmentRequestValidationService departmentRequestValidationService,
            IAuditLogService auditLogService)
        {
            _context = context;
            _userManager = userManager;
            _documentService = documentService;
            _departmentRequestValidationService = departmentRequestValidationService;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Create a document access request from the institution portal (no JWT required).
        /// Uses session token authentication.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("institution-access/requests")]
        public async Task<IActionResult> CreateInstitutionRequest(
            [FromQuery] string token,
            [FromBody] InstitutionDocumentRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Unauthorized(new { error = "Session token is required." });

            if (dto == null)
                return BadRequest(new { error = "Request data is required." });

            // Validate session token
            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && 
                                          !st.IsRevoked && 
                                          st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var institutionId = sessionToken.InstitutionId;

            // Validate request type and target
            if (string.IsNullOrWhiteSpace(dto.RequestType) || 
                (dto.RequestType != "Department" && dto.RequestType != "Individual"))
                return BadRequest(new { error = "RequestType must be either 'Department' or 'Individual'." });

            // Validate requested documents
            var requestedDocumentTypeIds = dto.RequestedDocuments
                .Select(r => r.DocumentTypeId)
                .Distinct()
                .ToList();

            if (!requestedDocumentTypeIds.Any())
                return BadRequest(new { error = "At least one document type must be requested." });

            // Validate request based on type
            Department? targetDepartment = null;
            User? targetUser = null;

            if (dto.RequestType == "Department")
            {
                if (!dto.TargetDepartmentId.HasValue || dto.TargetDepartmentId <= 0)
                    return BadRequest(new { error = "TargetDepartmentId is required for department requests." });

                targetDepartment = await _context.Departments
                    .Include(d => d.Branch)
                    .FirstOrDefaultAsync(d => d.DepartmentId == dto.TargetDepartmentId.Value);

                if (targetDepartment == null)
                    return NotFound(new { error = "Target department not found." });

                // Verify requested document types belong to department
                var departmentRequiredDocTypeIds = await _context.DepartmentDocumentTypes
                    .Where(ddt => ddt.DepartmentId == targetDepartment.DepartmentId)
                    .Select(ddt => ddt.DocumentTypeId)
                    .ToListAsync();

                if (!departmentRequiredDocTypeIds.Any())
                    return BadRequest(new { error = "Target department has no required document types configured." });

                var validationResult = _departmentRequestValidationService.ValidateRequestedDocumentTypes(
                    departmentRequiredDocTypeIds,
                    requestedDocumentTypeIds);

                if (!validationResult.IsValid)
                {
                    return BadRequest(new
                    {
                        error = "One or more requested document types do not belong to the target department's required documents.",
                        invalidDocumentTypeIds = validationResult.InvalidDocumentTypeIds,
                        allowedDocumentTypeIds = validationResult.AllowedDocumentTypeIds
                    });
                }
            }
            else // Individual request
            {
                if (string.IsNullOrWhiteSpace(dto.TargetUserId))
                    return BadRequest(new { error = "TargetUserId is required for individual requests." });

                targetUser = await ResolveUserAsync(dto.TargetUserId);
                if (targetUser == null)
                    return NotFound(new { error = "Target user not found." });
            }

            var defaultRule = await EnsureDefaultFicaRuleAsync();

            // Create the enquiry request
            var request = new InstitutionEnquiryRequest
            {
                InstitutionId = institutionId,
                RequestType = dto.RequestType,
                TargetDepartmentId = targetDepartment?.DepartmentId,
                TargetUserId = targetUser?.Id,
                PurposeNote = dto.PurposeNote?.Trim() ?? string.Empty,
                SubmissionDeadline = dto.SubmissionDeadline,
                ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? null : dto.ReferenceNumber.Trim(),
                Status = dto.RequestType == "Department" ? "Department_Pending" : "Pending",
                RequestDate = DateTimeOffset.UtcNow
            };

            _context.InstitutionEnquiryRequests.Add(request);
            await _context.SaveChangesAsync();

            try
            {
                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    InstitutionId = institutionId,
                    ActionCode = "INSTITUTION_REQUEST_CREATED",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = $"Institution portal request {request.EnquiryRequestId} created for institution {institutionId}.",
                    TableAffected = "InstitutionEnquiryRequests",
                    RecordID = request.EnquiryRequestId
                });
            }
            catch
            {
                // Audit failure should not break institution portal request submission.
            }

            // Add requested document types
            foreach (var requestedDocument in dto.RequestedDocuments.DistinctBy(r => r.DocumentTypeId))
            {
                var documentTypeExists = await _context.DocumentTypes
                    .AnyAsync(dt => dt.DocumentTypeId == requestedDocument.DocumentTypeId);

                if (!documentTypeExists)
                    return BadRequest(new { error = $"Document type {requestedDocument.DocumentTypeId} does not exist." });

                var ficaRuleId = requestedDocument.FICARuleId;

                if (ficaRuleId.HasValue)
                {
                    var ficaRuleExists = await _context.FICARules
                        .AnyAsync(fr => fr.RuleId == ficaRuleId.Value);

                    if (!ficaRuleExists)
                        return BadRequest(new { error = $"FICA rule {ficaRuleId.Value} does not exist." });
                }
                else
                {
                    ficaRuleId = defaultRule.RuleId;
                }

                _context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
                {
                    EnquiryRequestId = request.EnquiryRequestId,
                    DocumentTypeId = requestedDocument.DocumentTypeId,
                    FICARuleId = ficaRuleId.Value,
                    isMandatory = requestedDocument.IsMandatory
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                request.EnquiryRequestId,
                request.InstitutionId,
                request.RequestType,
                request.TargetDepartmentId,
                request.TargetUserId,
                request.Status,
                submissionDeadline = request.SubmissionDeadline,
                referenceNumber = request.ReferenceNumber,
                request.RequestDate,
                RequestedDocumentTypeIds = requestedDocumentTypeIds,
                Message = dto.RequestType == "Department" 
                    ? "Request created and routed to department for review."
                    : "Request created and sent to document owner for approval."
            });
        }


        /// <summary>
        /// Returns the list of requests made by the institution identified by session token.
        /// Intended for the institution portal to view outgoing requests (both individual and department targets).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("institution-access/requests")]
        public async Task<IActionResult> GetInstitutionRequests([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var institutionId = sessionToken.InstitutionId;

            var requests = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.TargetDepartment)
                .Include(r => r.TargetUser)
                    .ThenInclude(u => u.Profile)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Where(r => r.InstitutionId == institutionId && (r.Status == "Pending" || r.Status == "Department_Pending"))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var requestStatuses = new Dictionary<int, (bool IsComplete, int MissingCount)>();
            foreach (var request in requests)
            {
                var checklist = await ComputeInstitutionRequestChecklistAsync(request);
                requestStatuses[request.EnquiryRequestId] = (checklist.IsComplete, checklist.MissingCount);
            }

            return Ok(requests.Select(r => new
            {
                r.EnquiryRequestId,
                r.InstitutionId,
                InstitutionName = r.Institution.InstitutionName,
                r.RequestType,
                Recipient = r.RequestType == "Department"
                    ? new { Type = "Department", Name = r.TargetDepartment?.DepartmentName ?? "-" }
                    : new { Type = "Individual", Name = r.TargetUser != null ? (string.IsNullOrWhiteSpace(r.TargetUser.Profile.FirstName) && string.IsNullOrWhiteSpace(r.TargetUser.Profile.LastName) ? (r.TargetUser.UserName ?? r.TargetUser.Id) : (r.TargetUser.Profile.FirstName + " " + r.TargetUser.Profile.LastName).Trim()) : (r.TargetUserId ?? "-") },
                r.Status,
                r.PurposeNote,
                r.RequestDate,
                Documents = r.RequestedDocumentTypes.Select(d => new
                {
                    d.DocumentTypeId,
                    DocumentTypeName = d.DocumentType.TypeName,
                    d.isMandatory
                }),
                IsComplete = requestStatuses[r.EnquiryRequestId].IsComplete,
                MissingCount = requestStatuses[r.EnquiryRequestId].MissingCount
            }).ToList());
        }

        [AllowAnonymous]
        [HttpGet("institution-access/requests/{requestId:int}")]
        public async Task<IActionResult> GetInstitutionRequest([FromRoute] int requestId, [FromQuery] string token)
        {
            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);
            if (sessionToken == null) return Unauthorized(new { error = "Invalid or expired session token." });

            var request = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.TargetDepartment)
                .Include(r => r.TargetUser).ThenInclude(u => u.Profile)
                .Include(r => r.RequestedDocumentTypes).ThenInclude(rdt => rdt.DocumentType)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId && r.InstitutionId == sessionToken.InstitutionId);
            if (request == null) return NotFound(new { error = "Request not found." });

            return Ok(new
            {
                request.EnquiryRequestId,
                request.InstitutionId,
                request.RequestType,
                request.TargetDepartmentId,
                request.TargetUserId,
                RecipientName = request.RequestType == "Department"
                    ? request.TargetDepartment?.DepartmentName
                    : request.TargetUser?.Profile == null ? request.TargetUserId : $"{request.TargetUser.Profile.FirstName} {request.TargetUser.Profile.LastName}".Trim(),
                request.Status,
                request.PurposeNote,
                request.SubmissionDeadline,
                request.ReferenceNumber,
                Documents = request.RequestedDocumentTypes.Select(d => new
                {
                    d.DocumentTypeId,
                    DocumentTypeName = d.DocumentType.TypeName,
                    d.isMandatory
                })
            });
        }

        [AllowAnonymous]
        [HttpPut("institution-access/requests/{requestId:int}")]
        public async Task<IActionResult> UpdateInstitutionRequest(
            [FromRoute] int requestId,
            [FromQuery] string token,
            [FromBody] UpdateInstitutionDocumentRequestDto dto)
        {
            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);
            if (sessionToken == null) return Unauthorized(new { error = "Invalid or expired session token." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.PurposeNote) || !dto.RequestedDocuments.Any())
                return BadRequest(new { error = "A justification and at least one requested document are required." });

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.RequestedDocumentTypes)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId && r.InstitutionId == sessionToken.InstitutionId);

            if (request == null) return NotFound(new { error = "Request not found." });

            if (request.Status != "Pending" && request.Status != "Department_Pending")
                return Conflict(new { error = "Only pending requests can be edited." });

            var documentTypeIds = dto.RequestedDocuments.Select(d => d.DocumentTypeId).Distinct().ToList();
            var validDocumentTypeIds = await _context.DocumentTypes
                .Where(d => documentTypeIds.Contains(d.DocumentTypeId))
                .Select(d => d.DocumentTypeId)
                .ToListAsync();
            if (validDocumentTypeIds.Count != documentTypeIds.Count)
                return BadRequest(new { error = "One or more requested document types are invalid." });

            request.PurposeNote = dto.PurposeNote.Trim();
            request.SubmissionDeadline = dto.SubmissionDeadline;
            request.ReferenceNumber = dto.ReferenceNumber?.Trim();
            _context.InstitutionRequestedDocumentTypes.RemoveRange(request.RequestedDocumentTypes);

            var defaultRule = await EnsureDefaultFicaRuleAsync();
            foreach (var item in dto.RequestedDocuments.DistinctBy(d => d.DocumentTypeId))
                _context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
                {
                    EnquiryRequestId = requestId,
                    DocumentTypeId = item.DocumentTypeId,
                    FICARuleId = item.FICARuleId ?? defaultRule.RuleId,
                    isMandatory = item.IsMandatory
                });
            await _context.SaveChangesAsync();
            return Ok(new { request.EnquiryRequestId, request.Status, requestedDocumentTypeIds = documentTypeIds });
        }

        /// <summary>
        /// Revoke an outgoing institution request (institution portal).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("institution-access/requests/{requestId:int}/revoke")]
        public async Task<IActionResult> RevokeInstitutionRequest([FromRoute] int requestId, [FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var request = await _context.InstitutionEnquiryRequests
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            if (request.InstitutionId != sessionToken.InstitutionId)
                return Forbid();

            if (request.Status != "Pending" && request.Status != "Department_Pending")
                return Conflict(new { error = "Only pending requests can be revoked." });

            request.Status = "Revoked";
            request.RespondedAt = DateTime.UtcNow;

            if (request.AccessToken != null)
                request.AccessToken.IsRevoked = true;

            await _context.SaveChangesAsync();

            try
            {
                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    InstitutionId = sessionToken.InstitutionId,
                    ActionCode = "REQUEST_REVOKED",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = $"Institution revoked request {request.EnquiryRequestId}.",
                    TableAffected = "InstitutionEnquiryRequests",
                    RecordID = request.EnquiryRequestId
                });
            }
            catch
            {
                // Ignore audit failures
            }

            return Ok(new { message = "Request revoked.", requestId = request.EnquiryRequestId, status = request.Status });
        }

        [AllowAnonymous]
        [HttpGet("institution-access/requests/{requestId:int}/checklist")]
        public async Task<IActionResult> GetInstitutionRequestChecklist([FromRoute] int requestId, [FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var request = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.TargetDepartment)
                .Include(r => r.TargetUser)
                    .ThenInclude(u => u.Profile)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId && r.InstitutionId == sessionToken.InstitutionId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            var checklist = await ComputeInstitutionRequestChecklistAsync(request);

            return Ok(new
            {
                request.EnquiryRequestId,
                request.RequestType,
                request.Status,
                Recipient = request.RequestType == "Department"
                    ? new { Type = "Department", Name = request.TargetDepartment?.DepartmentName ?? "-" }
                    : new { Type = "Individual", Name = request.TargetUser != null ? (string.IsNullOrWhiteSpace(request.TargetUser.Profile.FirstName) && string.IsNullOrWhiteSpace(request.TargetUser.Profile.LastName) ? (request.TargetUser.UserName ?? request.TargetUser.Id) : (request.TargetUser.Profile.FirstName + " " + request.TargetUser.Profile.LastName).Trim()) : (request.TargetUserId ?? "-") },
                checklist.IsComplete,
                checklist.MissingCount,
                checklist.RequestedDocumentStatuses
            });
        }

        private async Task<(bool IsComplete, int MissingCount, object RequestedDocumentStatuses)> ComputeInstitutionRequestChecklistAsync(InstitutionEnquiryRequest request)
        {
            var requestedDocumentTypeIds = request.RequestedDocumentTypes
                .Select(rdt => rdt.DocumentTypeId)
                .Distinct()
                .ToList();

            var documentGroups = new List<(int DocumentTypeId, string DocumentTypeName, bool IsMandatory, List<(string CurrentStatus, int DocumentId)> Documents)>();

            if (request.RequestType == "Individual" && !string.IsNullOrWhiteSpace(request.TargetUserId))
            {
                var userDocuments = await _context.Documents
                    .AsNoTracking()
                    .Where(d => d.UserId == request.TargetUserId && d.CurrentStatus != "Deleted" && requestedDocumentTypeIds.Contains(d.DocumentTypeId))
                    .Select(d => new { d.DocumentTypeId, d.CurrentStatus, d.DocumentId })
                    .ToListAsync();

                foreach (var requested in request.RequestedDocumentTypes)
                {
                    var docs = userDocuments
                        .Where(d => d.DocumentTypeId == requested.DocumentTypeId)
                        .Select(d => (d.CurrentStatus, d.DocumentId))
                        .ToList();

                    documentGroups.Add((requested.DocumentTypeId, requested.DocumentType.TypeName, requested.isMandatory, docs));
                }
            }
            else if (request.RequestType == "Department" && request.TargetDepartmentId.HasValue)
            {
                var departmentUsers = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.DepartmentId == request.TargetDepartmentId.Value)
                    .Select(u => u.Id)
                    .ToListAsync();

                var departmentDocuments = await _context.Documents
                    .AsNoTracking()
                    .Where(d => departmentUsers.Contains(d.UserId) && d.CurrentStatus != "Deleted" && requestedDocumentTypeIds.Contains(d.DocumentTypeId))
                    .Select(d => new { d.DocumentTypeId, d.CurrentStatus, d.DocumentId })
                    .ToListAsync();

                foreach (var requested in request.RequestedDocumentTypes)
                {
                    var docs = departmentDocuments
                        .Where(d => d.DocumentTypeId == requested.DocumentTypeId)
                        .Select(d => (d.CurrentStatus, d.DocumentId))
                        .ToList();

                    documentGroups.Add((requested.DocumentTypeId, requested.DocumentType.TypeName, requested.isMandatory, docs));
                }
            }
            else
            {
                foreach (var requested in request.RequestedDocumentTypes)
                {
                    documentGroups.Add((requested.DocumentTypeId, requested.DocumentType.TypeName, requested.isMandatory, new List<(string, int)>()));
                }
            }

            var requestedDocumentStatuses = documentGroups.Select(group =>
            {
                var hasNonRejected = group.Documents.Any(d => !string.Equals(d.CurrentStatus, "Rejected", StringComparison.OrdinalIgnoreCase));
                var hasRejectedOnly = group.Documents.Any() && !hasNonRejected;
                var state = hasNonRejected ? "Uploaded" : hasRejectedOnly ? "Rejected" : "Missing";
                return new
                {
                    group.DocumentTypeId,
                    group.DocumentTypeName,
                    group.IsMandatory,
                    State = state,
                    IsUploaded = state == "Uploaded",
                    IsRejected = state == "Rejected",
                    UploadCount = group.Documents.Count
                };
            }).ToList();

            var missingCount = requestedDocumentStatuses.Count(s => s.State != "Uploaded");
            var isComplete = requestedDocumentStatuses.All(s => s.State == "Uploaded");
            return (isComplete, missingCount, requestedDocumentStatuses);
        }

        /// <summary>
        /// Return departments for the institution identified by session token.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("institution-access/requests/departments")]
        public async Task<IActionResult> GetInstitutionDepartments([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            // Return all departments (global), these are created by system/super-admin
            var departments = await _context.Departments
                .Include(d => d.Branch)
                .AsNoTracking()
                .Select(d => new
                {
                    departmentId = d.DepartmentId,
                    departmentName = d.DepartmentName
                })
                .OrderBy(d => d.departmentName)
                .ToListAsync();

            if (!departments.Any())
            {
                return Ok(new
                {
                    departments,
                    warning = "No departments found. Contact administrator to configure departments."
                });
            }

            return Ok(departments);
        }

        /// <summary>
        /// Return institution users (members) for the institution identified by session token.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("institution-access/requests/users")]
        public async Task<IActionResult> GetInstitutionUsers([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            // Return all registered users who have the Document Owner role.
            // Resolve the role first (by NormalizedName) then match UserRoles by RoleId to avoid navigation/translation edge cases.
            var docRole = await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "DOCUMENT OWNER");
            // If exact normalized lookup fails, try more permissive matches to handle unexpected DB values
            if (docRole == null)
            {
                docRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.NormalizedName != null && r.NormalizedName.Contains("DOCUMENT") && r.NormalizedName.Contains("OWNER"));
            }
            if (docRole == null)
            {
                docRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Name != null && r.Name.Contains("Document") && r.Name.Contains("Owner"));
            }

            List<object> members;

            if (docRole != null)
            {
                var documentOwnerUserIds = _context.Set<IdentityUserRole<string>>()
                    .Where(userRole => userRole.RoleId == docRole.Id)
                    .Select(userRole => userRole.UserId);

                members = await _context.Users
                    .Include(u => u.Profile)
                    .Where(u => documentOwnerUserIds.Contains(u.Id))
                    .Select(u => new
                    {
                        userId = u.Id,
                        userName = u.UserName,
                        displayName = !string.IsNullOrWhiteSpace(u.Profile.FirstName) || !string.IsNullOrWhiteSpace(u.Profile.LastName)
                            ? (u.Profile.FirstName + " " + u.Profile.LastName).Trim()
                            : (string.IsNullOrWhiteSpace(u.UserName) ? u.Id : u.UserName)
                    })
                    .Distinct()
                    .OrderBy(u => u.displayName)
                    .ToListAsync<object>();
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Document Owner role is not configured."
                });
            }

            return Ok(members);
        }

        /// <summary>
        /// Get recipient document types for the selected request type and recipient.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("institution-access/requests/document-types")]
        public async Task<IActionResult> GetInstitutionRecipientDocumentTypes(
            [FromQuery] string token,
            [FromQuery] string requestType,
            [FromQuery] string recipientId)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            if (string.IsNullOrWhiteSpace(requestType) ||
                (requestType != "Department" && requestType != "Individual"))
            {
                return BadRequest(new { error = "RequestType must be either 'Department' or 'Individual'." });
            }

            if (string.IsNullOrWhiteSpace(recipientId))
                return BadRequest(new { error = "RecipientId is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var institutionId = sessionToken.InstitutionId;

            if (requestType == "Department")
            {
                if (!int.TryParse(recipientId, out var departmentId) || departmentId <= 0)
                    return BadRequest(new { error = "RecipientId must be a valid department id." });

                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);

                if (department == null)
                    return NotFound(new { error = "Target department not found." });

                // For department requests, show the company-level required documents
                // (e.g. entity type 'Company' requirements) filtered by what the
                // department actually supports (DepartmentDocumentTypes).
                // Return company-level required documents (EntityTypeId == 3).
                // Previously we filtered these by DepartmentDocumentTypes; the UI
                // expects the full company requirement set (11..19), so return
                // the RequiredDocuments for the Company entity type.
                var requiredDocs = await _context.RequiredDocuments
                    .Where(rd => rd.EntityTypeId == 3)
                    .Include(rd => rd.DocumentType)
                    .AsNoTracking()
                    .OrderBy(rd => rd.IsMandatory ? 0 : 1)
                    .ThenBy(rd => rd.DocumentType.TypeName)
                    .Select(rd => new
                    {
                        documentTypeId = rd.DocumentTypeId,
                        typeName = rd.DocumentType.TypeName,
                        description = rd.DocumentType.Description,
                        isMandatory = rd.IsMandatory,
                        requirementNote = rd.Description ?? rd.DocumentType.Description
                    })
                    .ToListAsync();

                return Ok(new
                {
                    documentTypes = requiredDocs
                });
            }

            var targetUser = await ResolveUserAsync(recipientId);
            if (targetUser == null)
                return NotFound(new { error = "Target user not found." });

            var userWithEntityType = await _context.Users
                .Include(u => u.EntityType)
                .FirstOrDefaultAsync(u => u.Id == targetUser.Id);

            if (userWithEntityType == null)
                return NotFound(new { error = "Target user not found." });

            if (!userWithEntityType.EntityTypeId.HasValue)
            {
                // If no entity type is assigned, return the user's currently uploaded document types as a fallback.
                var fallbackDocumentTypes = await _context.Documents
                    .AsNoTracking()
                    .Where(d => d.UserId == targetUser.Id && d.CurrentStatus != "Deleted")
                    .Join(
                        _context.DocumentTypes,
                        d => d.DocumentTypeId,
                        dt => dt.DocumentTypeId,
                        (d, dt) => new
                        {
                            d.DocumentTypeId,
                            dt.TypeName,
                            dt.Description
                        })
                    .GroupBy(x => x.DocumentTypeId)
                    .Select(g => new
                    {
                        documentTypeId = g.Key,
                        typeName = g.Select(x => x.TypeName).FirstOrDefault(),
                        description = g.Select(x => x.Description).FirstOrDefault(),
                        isMandatory = false,
                        requirementNote = g.Select(x => x.Description).FirstOrDefault()
                    })
                    .OrderBy(x => x.typeName)
                    .ToListAsync();

                return Ok(new
                {
                    documentTypes = fallbackDocumentTypes,
                    warning = "Target user has not selected an entity type. Returning currently uploaded document types as a fallback."
                });
            }

            var requiredDocumentTypes = await _context.RequiredDocuments
                .AsNoTracking()
                .Where(rd => rd.EntityTypeId == userWithEntityType.EntityTypeId)
                .Include(rd => rd.DocumentType)
                .OrderBy(rd => rd.IsMandatory ? 0 : 1)
                .ThenBy(rd => rd.DocumentType.TypeName)
                .Select(rd => new
                {
                    documentTypeId = rd.DocumentTypeId,
                    typeName = rd.DocumentType.TypeName,
                    description = rd.DocumentType.Description,
                    isMandatory = rd.IsMandatory,
                    requirementNote = rd.Description ?? rd.DocumentType.Description
                })
                .ToListAsync();

            return Ok(new { documentTypes = requiredDocumentTypes });
        }

        /// <summary>
        /// Get pending requests for the current user (for document owners to review)
        /// </summary>
        [HttpGet("document-access-requests/pending")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, "Super Admin");

            var requestsQuery = _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.TargetUser)
                    .ThenInclude(u => u.Profile)
                .Include(r => r.TargetDepartment)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Where(r => r.Status == "Pending");

            if (!isSuperAdmin)
            {
                requestsQuery = requestsQuery.Where(r => r.TargetUserId == currentUser.Id);
            }

            var requests = await requestsQuery
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.EnquiryRequestId,
                    r.InstitutionId,
                    InstitutionName = r.Institution.InstitutionName,
                    r.TargetUserId,
                    SenderName = r.Institution.InstitutionName,
                    SenderType = "Institution",
                    RecipientName = r.RequestType == "Department"
                        ? (r.TargetDepartment != null ? r.TargetDepartment.DepartmentName : "-")
                        : (!string.IsNullOrWhiteSpace(r.TargetUser!.Profile!.FirstName) || !string.IsNullOrWhiteSpace(r.TargetUser.Profile.LastName)
                            ? (r.TargetUser.Profile.FirstName + " " + r.TargetUser.Profile.LastName).Trim()
                            : (string.IsNullOrWhiteSpace(r.TargetUser.UserName) ? r.TargetUser.Id : r.TargetUser.UserName)),
                    RecipientType = r.RequestType == "Department" ? "Department" : "Individual",
                    r.Status,
                    r.PurposeNote,
                    r.RequestDate,
                    Documents = r.RequestedDocumentTypes.Select(d => new
                    {
                        d.DocumentTypeId,
                        DocumentTypeName = d.DocumentType.TypeName,
                        d.isMandatory,
                        d.FICARuleId
                    })
                })
                .ToListAsync();

            return Ok(requests);
        }

        /// <summary>
        /// Get pending department requests for department admins
        /// </summary>
        [HttpGet("department-access-requests/pending")]
        [Authorize(Policy = "Documents.Manage")]
        public async Task<IActionResult> GetPendingDepartmentRequests()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, "Super Admin");
            var isDepartmentAdmin = await _userManager.IsInRoleAsync(currentUser, "Department Admin");

            if (!isSuperAdmin && !isDepartmentAdmin)
                return Forbid();

            var userDepartments = await _context.Users
                .Where(u => u.Id == currentUser.Id)
                .Include(u => u.Department)
                .Select(u => u.Department!.DepartmentId)
                .ToListAsync();

            var requestsQuery = _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.TargetDepartment)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Where(r => r.RequestType == "Department" && r.Status == "Department_Pending");

            if (!isSuperAdmin)
            {
                if (!userDepartments.Any())
                    return Ok(new object[] { }); // No departments to manage

                requestsQuery = requestsQuery.Where(r => userDepartments.Contains(r.TargetDepartmentId!.Value));
            }

            var requests = await requestsQuery
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.EnquiryRequestId,
                    r.InstitutionId,
                    InstitutionName = r.Institution.InstitutionName,
                    r.TargetDepartmentId,
                    DepartmentName = r.TargetDepartment!.DepartmentName,
                    SenderName = r.Institution.InstitutionName,
                    SenderType = "Institution",
                    RecipientName = r.TargetDepartment != null ? r.TargetDepartment.DepartmentName : "-",
                    RecipientType = "Department",
                    r.Status,
                    r.PurposeNote,
                    r.RequestDate,
                    Documents = r.RequestedDocumentTypes.Select(d => new
                    {
                        d.DocumentTypeId,
                        DocumentTypeName = d.DocumentType.TypeName,
                        d.isMandatory
                    })
                })
                .ToListAsync();

            return Ok(requests);
        }

        /// <summary>
        /// Department admin routes a request to a specific document owner
        /// </summary>
        [HttpPost("department-access-requests/{requestId:int}/route-to-owner")]
        [Authorize(Policy = "Documents.Manage")]
        public async Task<IActionResult> RouteRequestToOwner(
            [FromRoute] int requestId,
            [FromBody] RouteRequestToOwnerDto dto)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.TargetUserId))
                return BadRequest(new { error = "Target user ID is required." });

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.TargetDepartment)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            // Verify department admin is managing the target department
            if (!currentUser.DepartmentId.HasValue || currentUser.DepartmentId != request.TargetDepartmentId)
                return Forbid();

            if (request.Status != "Department_Pending")
                return Conflict(new { error = "Request must be in Department_Pending status to route to an owner." });

            // Verify target user exists and belongs to the department
            var targetUser = await ResolveUserAsync(dto.TargetUserId);
            if (targetUser == null)
                return NotFound(new { error = "Target user not found." });

            if (targetUser.DepartmentId != request.TargetDepartmentId)
                return BadRequest(new { error = "Target user does not belong to the same department." });

            // The owner may not have uploaded all requested document types yet — routing the
            // request to them becomes the requirement for them to upload what's missing.

            // Route the request to the owner
            request.TargetUserId = targetUser.Id;
            request.Status = "Pending";
            request.ApprovedByUserId = currentUser.Id;
            request.UserResponseNote = dto.AdminNote;

            await _context.SaveChangesAsync();

            await _auditLogService.CreateAuditLogAsync(new AuditLog
            {
                UserId = currentUser.Id,
                ActionCode = "REQUEST_ROUTED_TO_OWNER",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Request {request.EnquiryRequestId} routed to document owner {targetUser.UserName} by {currentUser.UserName}.",
                TableAffected = "InstitutionEnquiryRequests",
                RecordID = request.EnquiryRequestId
            });

            return Ok(new
            {
                request.EnquiryRequestId,
                request.Status,
                RoutedTo = new { targetUser.Id, targetUser.UserName },
                Message = "Request successfully routed to document owner."
            });
        }

        [HttpPost("document-access-requests/{requestId:int}/approve")]
        public async Task<IActionResult> Approve([FromRoute] int requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.Institution)
                .Include(r => r.TargetUser)
                .Include(r => r.TargetDepartment)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Include(r => r.AccessToken)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            // Only document owners can approve (not department admins)
            if (request.TargetUserId != currentUser.Id || request.Status != "Pending")
                return Forbid();

            if (request.Status == "Denied")
                return Conflict(new { error = "This request has already been denied." });

            if (request.AccessToken != null &&
                !request.AccessToken.IsRevoked &&
                request.AccessToken.ExpiryTimeStamp > DateTimeOffset.UtcNow)
            {
                return Ok(new
                {
                    request.EnquiryRequestId,
                    request.Status,
                    Token = request.AccessToken.TokenString,
                    ExpiresAt = request.AccessToken.ExpiryTimeStamp,
                    Message = "Request was already approved. Returning the active token."
                });
            }

            var requestedDocumentTypeIds = request.RequestedDocumentTypes
                .Select(rdt => rdt.DocumentTypeId)
                .Distinct()
                .ToList();

            var approvedDocuments = await _context.Documents
                .Where(d => d.UserId == request.TargetUserId
                    && d.CurrentStatus != "Deleted"
                    && requestedDocumentTypeIds.Contains(d.DocumentTypeId))
                .ToListAsync();

            var uploadedDocumentTypeIds = approvedDocuments.Select(d => d.DocumentTypeId).Distinct().ToList();
            var missingDocumentTypeIds = requestedDocumentTypeIds.Except(uploadedDocumentTypeIds).ToList();

            if (missingDocumentTypeIds.Any())
            {
                return BadRequest(new
                {
                    error = "All requested document types must be uploaded before this request can be approved.",
                    missingDocumentTypeIds
                });
            }

            var existingToken = await _context.AccessTokens
                .FirstOrDefaultAsync(at => at.UserId == request.TargetUserId);

            if (existingToken != null)
                existingToken.IsRevoked = true;

            var expiry = DateTimeOffset.UtcNow.AddHours(48);
            var tokenString = GenerateTokenString();

            var accessToken = new AccessToken
            {
                EnquiryRequestId = request.EnquiryRequestId,
                TokenString = tokenString,
                ExpiryTimeStamp = expiry,
                IsRevoked = false,
                UserId = request.TargetUserId,
                institutionEnquiryRequest = request
            };


            request.Status = "Approved";
            request.RespondedAt = DateTime.UtcNow;
            request.ApprovedByUserId = currentUser.Id;

            _context.AccessTokens.Add(accessToken);

            foreach (var document in approvedDocuments)
            {
                _context.DocumentAccessApprovals.Add(new DocumentAccessApproval
                {
                    EnquiryRequestId = request.EnquiryRequestId,
                    DocumentId = document.DocumentId,
                    ApprovedByUserId = currentUser.Id,
                    ApprovedAt = DateTime.UtcNow,
                    ExpiresAt = expiry.UtcDateTime,

                    IsRevoked = false
                });
            }

            await _context.SaveChangesAsync();

            await _auditLogService.CreateAuditLogAsync(new AuditLog
            {
                UserId = currentUser.Id,
                ActionCode = "REQUEST_APPROVED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Request {request.EnquiryRequestId} approved by {currentUser.UserName}.",
                TableAffected = "InstitutionEnquiryRequests",
                RecordID = request.EnquiryRequestId
            });

            return Ok(new
            {
                request.EnquiryRequestId,
                request.Status,
                Token = tokenString,
                ExpiresAt = expiry,
                DocumentIds = approvedDocuments.Select(d => d.DocumentId)
            });
        }

        [HttpPost("document-access-requests/{requestId:int}/deny")]
        public async Task<IActionResult> Deny([FromRoute] int requestId, [FromBody] ApproveRequestDto? dto = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.AccessToken)
                .Include(r => r.TargetDepartment)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            // Allow denial by document owner or department admin (for department requests)
            bool isOwner = request.TargetUserId == currentUser.Id && request.Status == "Pending";
            bool isDepartmentAdmin = request.RequestType == "Department" && 
                                     request.Status == "Department_Pending" && 
                                     currentUser.DepartmentId == request.TargetDepartmentId;

            if (!isOwner && !isDepartmentAdmin)
                return Forbid();

            request.Status = "Denied";
            request.RespondedAt = DateTime.UtcNow;
            request.ApprovedByUserId = currentUser.Id;
            request.UserResponseNote = dto?.UserResponseNote;

            if (request.AccessToken != null)
                request.AccessToken.IsRevoked = true;

            await _context.SaveChangesAsync();

            await _auditLogService.CreateAuditLogAsync(new AuditLog
            {
                UserId = currentUser.Id,
                ActionCode = "REQUEST_DENIED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Request {request.EnquiryRequestId} denied by {currentUser.UserName}.",
                TableAffected = "InstitutionEnquiryRequests",
                RecordID = request.EnquiryRequestId
            });

            return Ok(new { 
                message = "Request denied.",
                requestId = request.EnquiryRequestId,
                status = request.Status
            });
        }

        [AllowAnonymous]
        [HttpGet("institution-access/documents/{documentId:int}")]
        public async Task<IActionResult> DownloadWithInstitutionToken(
            [FromRoute] int documentId,
            [FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Unauthorized(new { error = "A valid access token or institution session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken != null)
            {
                var approval = await _context.DocumentAccessApprovals
                    .Include(daa => daa.Document)
                    .Include(daa => daa.InstitutionEnquiryRequest)
                    .ThenInclude(r => r.TargetUser)
                    .FirstOrDefaultAsync(daa => daa.DocumentId == documentId
                        && daa.InstitutionEnquiryRequest.InstitutionId == sessionToken.InstitutionId
                        && daa.InstitutionEnquiryRequest.Status == "Approved"
                        && !daa.IsRevoked
                        && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow));

                if (approval == null)
                    return Problem(detail: "Document is not approved for this institution or is no longer available.", statusCode: StatusCodes.Status403Forbidden);

                var document = approval.Document;
                if (document == null)
                    return NotFound();

                if (document.UserId != approval.InstitutionEnquiryRequest.TargetUserId)
                    return Problem(detail: "Document ownership does not match the approved request.", statusCode: StatusCodes.Status403Forbidden);

                var fileBytes = await _documentService.DownloadDocumentAsync(documentId, approval.InstitutionEnquiryRequest.TargetUserId);
                return File(fileBytes, "application/octet-stream", document.FileName);
            }

            var accessToken = await _context.AccessTokens
                .Include(at => at.institutionEnquiryRequest)
                    .ThenInclude(ier => ier.RequestedDocumentTypes)
                .FirstOrDefaultAsync(at => at.TokenString == token);

            if (accessToken == null ||
                accessToken.IsRevoked ||
                accessToken.ExpiryTimeStamp <= DateTimeOffset.UtcNow)
            {
                if (accessToken != null && !accessToken.IsRevoked)
                {
                    accessToken.IsRevoked = true;
                    await _context.SaveChangesAsync();
                }

                return Unauthorized(new { error = "The access token is invalid or has expired." });
            }

            var request = accessToken.institutionEnquiryRequest;
            if (request.Status != "Approved")
                return Forbid();

            var documentByToken = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (documentByToken == null)
                return NotFound();

            var documentApproved = await _context.DocumentAccessApprovals
                .AnyAsync(daa => daa.EnquiryRequestId == request.EnquiryRequestId
                    && daa.DocumentId == documentId
                    && !daa.IsRevoked
                    && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow));

            if (!documentApproved)
                return Forbid();

            if (documentByToken.UserId != request.TargetUserId)
                return Forbid();

            var fileBytesToken = await _documentService.DownloadDocumentAsync(documentId, request.TargetUserId);
            return File(fileBytesToken, "application/octet-stream", documentByToken.FileName);
        }

        [AllowAnonymous]
        [HttpPost("institution-access/documents/{documentId:int}/flag")]
        public async Task<IActionResult> FlagApprovedDocument(
            [FromRoute] int documentId,
            [FromQuery] string token,
            [FromBody] FlagApprovedDocumentDto dto)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Problem(detail: "A valid institution session token is required.", statusCode: StatusCodes.Status401Unauthorized);

            if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(new { error = "Flag reason is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Problem(detail: "Invalid or expired institution session token.", statusCode: StatusCodes.Status401Unauthorized);

            var approval = await _context.DocumentAccessApprovals
                .Include(daa => daa.InstitutionEnquiryRequest)
                .FirstOrDefaultAsync(daa => daa.DocumentId == documentId
                    && daa.InstitutionEnquiryRequest.InstitutionId == sessionToken.InstitutionId
                    && daa.InstitutionEnquiryRequest.Status == "Approved"
                    && !daa.IsRevoked
                    && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow));

            if (approval == null)
                return Problem(detail: "Document is not approved for this institution or is no longer available.", statusCode: StatusCodes.Status403Forbidden);

            var flag = new EnquiryFlag
            {
                DocumentId = documentId,
                EnquiryId = approval.EnquiryRequestId,
                FlagReason = dto.Reason.Trim(),
                IsResolved = false,
            };

            _context.EnquiryFlags.Add(flag);
            await _context.SaveChangesAsync();

            _context.AccessLists.Add(new AccessList
            {
                EnquiryRequestId = approval.EnquiryRequestId,
                DocumentId = documentId,
                EnquiryId = flag.EnquiryFlagId
            });

            await _context.SaveChangesAsync();

            try
            {
                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    InstitutionId = sessionToken.InstitutionId,
                    ActionCode = "DOCUMENT_FLAGGED",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = $"Approved document {documentId} was flagged by institution {sessionToken.InstitutionId}. Reason: {flag.FlagReason}",
                    TableAffected = "EnquiryFlags",
                    RecordID = flag.EnquiryFlagId
                });
            }
            catch
            {
                // Audit failure should not block the user action.
            }

            return Ok(new { message = "Document flagged and reason submitted." });
        }

        [AllowAnonymous]
        [HttpGet("institution-access/documents")]
        public async Task<IActionResult> GetApprovedInstitutionDocuments([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid access token or institution session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken != null)
            {
                var approvedDocumentList = await _context.DocumentAccessApprovals
                    .AsNoTracking()
                    .Include(daa => daa.Document)
                        .ThenInclude(d => d.DocumentType)
                    .Include(daa => daa.InstitutionEnquiryRequest)
                        .ThenInclude(r => r.TargetDepartment)
                    .Include(daa => daa.InstitutionEnquiryRequest)
                        .ThenInclude(r => r.TargetUser)
                            .ThenInclude(u => u.Profile)
                    .Where(daa => daa.InstitutionEnquiryRequest.InstitutionId == sessionToken.InstitutionId
                        && daa.InstitutionEnquiryRequest.Status == "Approved"
                        && !daa.IsRevoked
                        && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow))
                    .Select(daa => new
                    {
                        RequestId = daa.EnquiryRequestId,
                        RequestType = daa.InstitutionEnquiryRequest.RequestType,
                        RecipientName = daa.InstitutionEnquiryRequest.RequestType == "Department"
                            ? (daa.InstitutionEnquiryRequest.TargetDepartment != null ? daa.InstitutionEnquiryRequest.TargetDepartment.DepartmentName : "-")
                            : (daa.InstitutionEnquiryRequest.TargetUser != null
                                ? ((!string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile.FirstName) || !string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName))
                                    ? (daa.InstitutionEnquiryRequest.TargetUser.Profile.FirstName + " " + daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName).Trim()
                                    : (daa.InstitutionEnquiryRequest.TargetUser.UserName ?? daa.InstitutionEnquiryRequest.TargetUser.Id))
                                : (daa.InstitutionEnquiryRequest.TargetUserId ?? "-")),
                        daa.DocumentId,
                        DocumentName = daa.Document.FileName,
                        DocumentTypeName = daa.Document.DocumentType.TypeName,
                        ApprovedAt = daa.ApprovedAt,
                        ExpiresAt = daa.ExpiresAt
                    })
                    .ToListAsync();

                return Ok(approvedDocumentList);
            }

            var accessToken = await _context.AccessTokens
                .Include(at => at.institutionEnquiryRequest)
                    .ThenInclude(ier => ier.RequestedDocumentTypes)
                .Include(at => at.institutionEnquiryRequest)
                    .ThenInclude(r => r.Institution)
                .FirstOrDefaultAsync(at => at.TokenString == token);

            if (accessToken == null ||
                accessToken.IsRevoked ||
                accessToken.ExpiryTimeStamp <= DateTimeOffset.UtcNow)
            {
                if (accessToken != null && !accessToken.IsRevoked)
                {
                    accessToken.IsRevoked = true;
                    await _context.SaveChangesAsync();
                }

                return Unauthorized(new { error = "The access token is invalid or has expired." });
            }

            var request = accessToken.institutionEnquiryRequest;
            if (request.Status != "Approved")
                return Forbid();

            var approvedDocuments = await _context.DocumentAccessApprovals
                .AsNoTracking()
                .Include(daa => daa.Document)
                    .ThenInclude(d => d.DocumentType)
                .Include(daa => daa.InstitutionEnquiryRequest)
                    .ThenInclude(r => r.TargetDepartment)
                .Include(daa => daa.InstitutionEnquiryRequest)
                    .ThenInclude(r => r.TargetUser)
                        .ThenInclude(u => u.Profile)
                .Where(daa => daa.EnquiryRequestId == request.EnquiryRequestId
                    && !daa.IsRevoked
                    && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow))
                .Select(daa => new
                {
                    RequestId = daa.EnquiryRequestId,
                    RequestType = daa.InstitutionEnquiryRequest.RequestType,
                    RecipientName = daa.InstitutionEnquiryRequest.RequestType == "Department"
                        ? (daa.InstitutionEnquiryRequest.TargetDepartment != null ? daa.InstitutionEnquiryRequest.TargetDepartment.DepartmentName : "-")
                        : (daa.InstitutionEnquiryRequest.TargetUser != null
                            ? ((!string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile.FirstName) || !string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName))
                                ? (daa.InstitutionEnquiryRequest.TargetUser.Profile.FirstName + " " + daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName).Trim()
                                : (daa.InstitutionEnquiryRequest.TargetUser.UserName ?? daa.InstitutionEnquiryRequest.TargetUser.Id))
                            : (daa.InstitutionEnquiryRequest.TargetUserId ?? "-")),
                    daa.DocumentId,
                    DocumentName = daa.Document.FileName,
                    DocumentTypeName = daa.Document.DocumentType.TypeName,
                    ApprovedAt = daa.ApprovedAt,
                    ExpiresAt = daa.ExpiresAt
                })
                .ToListAsync();

            return Ok(approvedDocuments);
        }

        /// <summary>
        /// Returns recent approved and denied institution notifications for the session.
        /// This powers the institution portal notification bell.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("institution-access/notifications")]
        public async Task<IActionResult> GetInstitutionNotifications([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var notifications = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.TargetDepartment)
                .Include(r => r.TargetUser)
                    .ThenInclude(u => u.Profile)
                .Where(r => r.InstitutionId == sessionToken.InstitutionId
                    && (r.Status == "Approved" || r.Status == "Denied")
                    && r.RespondedAt != null)
                .OrderByDescending(r => r.RespondedAt)
                .Take(10)
                .Select(r => new
                {
                    r.EnquiryRequestId,
                    r.Status,
                    RequestType = r.RequestType,
                    RecipientName = r.RequestType == "Department"
                        ? (r.TargetDepartment != null ? r.TargetDepartment.DepartmentName : "-")
                        : (r.TargetUser != null
                            ? ((!string.IsNullOrWhiteSpace(r.TargetUser.Profile.FirstName) || !string.IsNullOrWhiteSpace(r.TargetUser.Profile.LastName))
                                ? (r.TargetUser.Profile.FirstName + " " + r.TargetUser.Profile.LastName).Trim()
                                : (r.TargetUser.UserName ?? r.TargetUserId ?? "-"))
                            : (r.TargetUserId ?? "-")),
                    Message = r.Status == "Approved"
                        ? "Your documents were approved and are available for download."
                        : "Your request was denied. Please contact the compliance team for more information.",
                    Timestamp = r.RespondedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        /// <summary>
        /// Returns request counts for an institution session identified by token.
        /// This is intended for the institution portal (no JWT required).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("institution-access/requests/summary")]
        public async Task<IActionResult> GetInstitutionAccessRequestSummary([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid session token is required." });

            var sessionToken = await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);

            if (sessionToken == null)
                return Unauthorized(new { error = "Invalid or expired session token." });

            var institutionId = sessionToken.InstitutionId;

            var summary = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Where(r => r.InstitutionId == institutionId)
                .GroupBy(r => 1)
                .Select(g => new
                {
                    PendingRequests = g.Count(r => r.Status == "Pending" || r.Status == "Department_Pending"),
                    ApprovedRequests = g.Count(r => r.Status == "Approved"),
                    DeniedRequests = g.Count(r => r.Status == "Denied")
                })
                .FirstOrDefaultAsync();

            return Ok(summary ?? new { PendingRequests = 0, ApprovedRequests = 0, DeniedRequests = 0 });
        }

        private async Task<User?> ResolveUserAsync(string identifier)
        {
            var user = await _userManager.FindByIdAsync(identifier);
            if (user != null)
                return user;

            user = await _userManager.FindByNameAsync(identifier);
            if (user != null)
                return user;

            return await _userManager.FindByEmailAsync(identifier);
        }

        private async Task<FICARule> EnsureDefaultFicaRuleAsync()
        {
            var defaultRule = await _context.FICARules
                .OrderBy(fr => fr.RuleId)
                .FirstOrDefaultAsync();

            if (defaultRule != null)
                return defaultRule;

            defaultRule = new FICARule
            {
                Description = "Default institution access request rule",
                ValidityMonths = 48
            };

            _context.FICARules.Add(defaultRule);
            await _context.SaveChangesAsync();
            return defaultRule;
        }

        private static string GenerateTokenString()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}
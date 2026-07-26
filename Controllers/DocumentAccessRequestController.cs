using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
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

        public DocumentAccessRequestsController(
            AppDbContext context,
            UserManager<User> userManager,
            IDocumentService documentService,
            DepartmentRequestValidationService departmentRequestValidationService)
        {
            _context = context;
            _userManager = userManager;
            _documentService = documentService;
            _departmentRequestValidationService = departmentRequestValidationService;
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

                // Verify department belongs to the institution
                if (targetDepartment.Branch.InstitutionId != institutionId)
                    return BadRequest(new { error = "Department does not belong to the specified institution." });

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
                Status = dto.RequestType == "Department" ? "Department_Pending" : "Pending",
                RequestDate = DateTimeOffset.UtcNow
            };

            _context.InstitutionEnquiryRequests.Add(request);
            await _context.SaveChangesAsync();

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
                submissionDeadline = dto.SubmissionDeadline,
                referenceNumber = dto.ReferenceNumber?.Trim(),
                request.RequestDate,
                RequestedDocumentTypeIds = requestedDocumentTypeIds,
                Message = dto.RequestType == "Department" 
                    ? "Request created and routed to department for review."
                    : "Request created and sent to document owner for approval."
            });
        }

        [HttpPost("institutions/{institutionId:int}/document-access-requests")]
        public async Task<IActionResult> CreateRequest(
            [FromRoute] int institutionId,
            [FromBody] InstitutionDocumentRequestDto dto)
        {
            var actor = await _userManager.GetUserAsync(User);
            if (actor == null)
                return Unauthorized();

            if (dto == null)
                return BadRequest(new { error = "Request data is required." });

            // Validate request type and target
            if (string.IsNullOrWhiteSpace(dto.RequestType) || 
                (dto.RequestType != "Department" && dto.RequestType != "Individual"))
                return BadRequest(new { error = "RequestType must be either 'Department' or 'Individual'." });

            // Verify institution membership
            var isMember = await _context.Institutions
                .Include(i => i.InstitutionMembers)
                .Where(i => i.InstitutionId == institutionId)
                .SelectMany(i => i.InstitutionMembers)
                .AnyAsync(im => im.UserId == actor.Id);

            if (!isMember)
                return Forbid();

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

                // Verify department belongs to the institution
                if (targetDepartment.Branch.InstitutionId != institutionId)
                    return BadRequest(new { error = "Department does not belong to the specified institution." });
            }
            else // Individual request
            {
                if (string.IsNullOrWhiteSpace(dto.TargetUserId))
                    return BadRequest(new { error = "TargetUserId is required for individual requests." });

                targetUser = await ResolveUserAsync(dto.TargetUserId);
                if (targetUser == null)
                    return NotFound(new { error = "Target user not found." });
            }

            // Validate requested documents
            var requestedDocumentTypeIds = dto.RequestedDocuments
                .Select(r => r.DocumentTypeId)
                .Distinct()
                .ToList();

            if (!requestedDocumentTypeIds.Any())
                return BadRequest(new { error = "At least one document type must be requested." });

            // If targeting individual, check if they have requested documents
            if (targetUser != null)
            {
                var targetDocumentTypeIds = await _context.Documents
                    .AsNoTracking()
                    .Where(d => d.UserId == targetUser.Id && d.CurrentStatus != "Deleted")
                    .Select(d => d.DocumentTypeId)
                    .Distinct()
                    .ToListAsync();

                var missingDocumentTypes = requestedDocumentTypeIds
                    .Except(targetDocumentTypeIds)
                    .ToList();

                if (missingDocumentTypes.Any())
                {
                    return BadRequest(new
                    {
                        error = "The target user does not currently have all requested document types.",
                        missingDocumentTypeIds = missingDocumentTypes
                    });
                }
            }
            else if (targetDepartment != null)
            {
                // Validate that requested document types belong to the department's required documents
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

                // For department requests, check if at least one user in the department has these documents
                var departmentUserIds = await _context.Users
                    .Where(u => u.DepartmentId == targetDepartment.DepartmentId)
                    .Select(u => u.Id)
                    .ToListAsync();

                if (!departmentUserIds.Any())
                    return BadRequest(new { error = "Target department has no users assigned." });

                var availableDocumentTypes = await _context.Documents
                    .AsNoTracking()
                    .Where(d => departmentUserIds.Contains(d.UserId) && d.CurrentStatus != "Deleted")
                    .Select(d => d.DocumentTypeId)
                    .Distinct()
                    .ToListAsync();

                // We don't strictly require all docs for department requests (they're not assigned to one person)
                // But we warn if some are missing
                var missingTypes = requestedDocumentTypeIds.Except(availableDocumentTypes).ToList();
                if (missingTypes.Any())
                {
                    return BadRequest(new
                    {
                        error = "Department members do not have all requested document types available.",
                        missingDocumentTypeIds = missingTypes
                    });
                }
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
                Status = dto.RequestType == "Department" ? "Department_Pending" : "Pending",
                RequestDate = DateTimeOffset.UtcNow
            };

            _context.InstitutionEnquiryRequests.Add(request);
            await _context.SaveChangesAsync();

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
                submissionDeadline = dto.SubmissionDeadline,
                referenceNumber = dto.ReferenceNumber?.Trim(),
                request.RequestDate,
                RequestedDocumentTypeIds = requestedDocumentTypeIds,
                Message = dto.RequestType == "Department" 
                    ? "Request created and routed to department for review."
                    : "Request created and sent to document owner for approval."
            });
        }

        /// <summary>
        /// Get request counts for the current institution.
        /// </summary>
        [HttpGet("institutions/{institutionId:int}/document-access-requests/summary")]
        public async Task<IActionResult> GetInstitutionRequestSummary([FromRoute] int institutionId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var isMember = await _context.Institutions
                .Include(i => i.InstitutionMembers)
                .Where(i => i.InstitutionId == institutionId)
                .SelectMany(i => i.InstitutionMembers)
                .AnyAsync(im => im.UserId == currentUser.Id);

            if (!isMember)
                return Forbid();

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

            var institutionId = sessionToken.InstitutionId;

            var departments = await _context.Departments
                .Include(d => d.Branch)
                .AsNoTracking()
                .Where(d => d.Branch.InstitutionId == institutionId)
                .Select(d => new
                {
                    departmentId = d.DepartmentId,
                    departmentName = d.DepartmentName
                })
                .ToListAsync();

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

            var institutionId = sessionToken.InstitutionId;

            var documentOwnerRoleId = await _context.Roles
                .Where(r => r.NormalizedName == "DOCUMENT OWNER")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(documentOwnerRoleId))
            {
                return Ok(new object[] { });
            }

            var institutionUserIds = _context.InstitutionMembers
                .Where(im => im.InstitutionId == institutionId)
                .Select(im => im.UserId);

            var members = await _context.Users
                .AsNoTracking()
                .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == documentOwnerRoleId)
                            && (institutionUserIds.Contains(u.Id) ||
                                (u.Department != null && u.Department.Branch.InstitutionId == institutionId)))
                .Select(u => new
                {
                    userId = u.Id,
                    userName = u.UserName,
                    displayName = string.IsNullOrWhiteSpace(u.UserName) ? u.Id : u.UserName
                })
                .Distinct()
                .ToListAsync();

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
                    .Include(d => d.Branch)
                    .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);

                if (department == null)
                    return NotFound(new { error = "Target department not found." });

                if (department.Branch?.InstitutionId != institutionId)
                    return BadRequest(new { error = "Department does not belong to the institution." });

                var requiredDocs = await _context.DepartmentDocumentTypes
                    .Where(ddt => ddt.DepartmentId == departmentId)
                    .Include(ddt => ddt.DocumentType)
                    .AsNoTracking()
                    .Select(ddt => new
                    {
                        documentTypeId = ddt.DocumentTypeId,
                        typeName = ddt.DocumentType.TypeName,
                        description = ddt.DocumentType.Description,
                        isMandatory = ddt.IsMandatory,
                        requirementNote = ddt.DocumentType.Description
                    })
                    .ToListAsync();

                return Ok(requiredDocs);
            }

            var targetUser = await ResolveUserAsync(recipientId);
            if (targetUser == null)
                return NotFound(new { error = "Target user not found." });

            var userDocumentTypes = await _context.Documents
                .AsNoTracking()
                .Where(d => d.UserId == targetUser.Id && d.CurrentStatus != "Deleted")
                .Include(d => d.DocumentType)
                .Select(d => new
                {
                    documentTypeId = d.DocumentTypeId,
                    typeName = d.DocumentType.TypeName,
                    description = d.DocumentType.Description,
                    isMandatory = false,
                    requirementNote = d.DocumentType.Description
                })
                .DistinctBy(d => d.documentTypeId)
                .ToListAsync();

            return Ok(userDocumentTypes);
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

            var requests = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Where(r => r.TargetUserId == currentUser.Id && r.Status == "Pending")
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.EnquiryRequestId,
                    r.InstitutionId,
                    InstitutionName = r.Institution.InstitutionName,
                    r.TargetUserId,
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
        [Authorize(Roles = "Department Admin")]
        public async Task<IActionResult> GetPendingDepartmentRequests()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            // Find departments where current user is admin
            var userDepartments = await _context.Users
                .Where(u => u.Id == currentUser.Id)
                .Include(u => u.Department)
                .Select(u => u.Department!.DepartmentId)
                .ToListAsync();

            if (!userDepartments.Any())
                return Ok(new object[] { }); // No departments to manage

            var requests = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.TargetDepartment)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Where(r => r.RequestType == "Department" && 
                           r.Status == "Department_Pending" &&
                           userDepartments.Contains(r.TargetDepartmentId!.Value))
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.EnquiryRequestId,
                    r.InstitutionId,
                    InstitutionName = r.Institution.InstitutionName,
                    r.TargetDepartmentId,
                    DepartmentName = r.TargetDepartment!.DepartmentName,
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
        [Authorize(Roles = "Department Admin")]
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

            // Verify target user has the requested documents
            var requestedDocTypes = request.RequestedDocumentTypes.Select(r => r.DocumentTypeId).ToList();
            var targetUserDocTypes = await _context.Documents
                .Where(d => d.UserId == targetUser.Id && d.CurrentStatus != "Deleted")
                .Select(d => d.DocumentTypeId)
                .Distinct()
                .ToListAsync();

            var missingTypes = requestedDocTypes.Except(targetUserDocTypes).ToList();
            if (missingTypes.Any())
            {
                return BadRequest(new
                {
                    error = "Target user does not have all requested document types.",
                    missingDocumentTypeIds = missingTypes
                });
            }

            // Route the request to the owner
            request.TargetUserId = targetUser.Id;
            request.Status = "Pending";
            request.ApprovedByUserId = currentUser.Id;
            request.UserResponseNote = dto.AdminNote;

            await _context.SaveChangesAsync();

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

            var approvedDocuments = await _context.Documents
                .Where(d => d.UserId == request.TargetUserId
                    && d.CurrentStatus != "Deleted"
                    && request.RequestedDocumentTypes
                        .Select(rdt => rdt.DocumentTypeId)
                        .Contains(d.DocumentTypeId))
                .ToListAsync();

            if (!approvedDocuments.Any())
                return BadRequest(new { error = "No matching documents were found to approve." });

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
                return Unauthorized(new { error = "A valid access token is required." });

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

            var document = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (document == null)
                return NotFound();

            var documentApproved = await _context.DocumentAccessApprovals
                .AnyAsync(daa => daa.EnquiryRequestId == request.EnquiryRequestId
                    && daa.DocumentId == documentId
                    && !daa.IsRevoked
                    && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow));

            if (!documentApproved)
                return Forbid();

            if (document.UserId != request.TargetUserId)
                return Forbid();

            var fileBytes = await _documentService.DownloadDocumentAsync(documentId, request.TargetUserId);
            return File(fileBytes, "application/octet-stream", document.FileName);
        }

        [AllowAnonymous]
        [HttpGet("institution-access/documents")]
        public async Task<IActionResult> GetApprovedInstitutionDocuments([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "A valid access token is required." });

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
                .Where(daa => daa.EnquiryRequestId == request.EnquiryRequestId
                    && !daa.IsRevoked
                    && (!daa.ExpiresAt.HasValue || daa.ExpiresAt.Value > DateTime.UtcNow))
                .Select(daa => new
                {
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
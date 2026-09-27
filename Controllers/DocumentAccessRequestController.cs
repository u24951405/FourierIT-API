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
        private readonly IComplianceService _complianceService;
        private readonly ISystemSettingsService _settings;
        private readonly IInAppNotificationService? _notifications;

        public DocumentAccessRequestsController(
            AppDbContext context,
            UserManager<User> userManager,
            IDocumentService documentService,
            DepartmentRequestValidationService departmentRequestValidationService,
            IAuditLogService auditLogService,
            IComplianceService complianceService,
            ISystemSettingsService? settings = null,
            IInAppNotificationService? notifications = null)
        {
            _context = context;
            _userManager = userManager;
            _documentService = documentService;
            _departmentRequestValidationService = departmentRequestValidationService;
            _auditLogService = auditLogService;
            _complianceService = complianceService;
            _settings = settings ?? new SystemSettingsService(context);
            _notifications = notifications;
        }

        private const string RequestsPage = "/documents/requests";

        /// <summary>Who a request is addressed to, for the institution. Users without a profile fall back to their username.</summary>
        private static string RecipientDisplayName(InstitutionEnquiryRequest request)
        {
            if (request.RequestType == "Department")
                return request.TargetDepartment?.DepartmentName ?? "-";

            var user = request.TargetUser;
            if (user == null) return request.TargetUserId ?? "-";

            var name = $"{user.Profile?.FirstName} {user.Profile?.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? user.UserName ?? user.Id : name;
        }

        /// <summary>The document types a department accepts requests for (set up by the Super Admin).</summary>
        private Task<List<int>> GetDepartmentDocumentTypeIdsAsync(int departmentId) =>
            _context.DepartmentDocumentTypes
                .Where(ddt => ddt.DepartmentId == departmentId)
                .Select(ddt => ddt.DocumentTypeId)
                .ToListAsync();

        /// <summary>Runs a notification step; a notification failure never undoes the action that raised it.</summary>
        private async Task TryNotifyAsync(Func<IInAppNotificationService, Task> notify)
        {
            if (_notifications == null) return;
            try
            {
                await notify(_notifications);
            }
            catch
            {
                // Notifications are best-effort.
            }
        }

        private async Task<string> GetInstitutionNameAsync(int institutionId) =>
            await _context.Institutions
                .AsNoTracking()
                .Where(i => i.InstitutionId == institutionId)
                .Select(i => i.InstitutionName)
                .FirstOrDefaultAsync() ?? "An institution";

        private static string DescribeRequest(InstitutionEnquiryRequest request) =>
            string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"request #{request.EnquiryRequestId}"
                : $"request {request.ReferenceNumber} (#{request.EnquiryRequestId})";

        /// <summary>The owner a request targets, or the admins of its department while it is still with the department.</summary>
        private async Task<List<string>> GetRequestRecipientsAsync(InstitutionEnquiryRequest request, IInAppNotificationService notifications)
        {
            if (!string.IsNullOrWhiteSpace(request.TargetUserId))
                return new List<string> { request.TargetUserId };

            return request.TargetDepartmentId == null
                ? new List<string>()
                : await notifications.GetUserIdsInRoleAsync("Department Admin", request.TargetDepartmentId);
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
                RequestDate = DateTimeOffset.UtcNow,
                // Sessions started before the email was stored fall back to the institution's latest invitation.
                RequesterEmail = sessionToken.Email ?? await _context.InstitutionInvitations
                    .Where(ii => ii.InstitutionId == institutionId)
                    .OrderByDescending(ii => ii.InvitationId)
                    .Select(ii => ii.Email)
                    .FirstOrDefaultAsync()
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

            await TryNotifyAsync(async notifications =>
            {
                var institutionName = await GetInstitutionNameAsync(institutionId);
                var count = requestedDocumentTypeIds.Count;
                var documents = $"{count} document{(count == 1 ? "" : "s")}";
                var deadline = request.SubmissionDeadline.HasValue ? $" Respond by {request.SubmissionDeadline.Value:d MMM yyyy}." : string.Empty;
                var purpose = string.IsNullOrWhiteSpace(request.PurposeNote) ? string.Empty : $" Purpose: {request.PurposeNote}.";

                if (targetUser != null)
                {
                    await notifications.NotifyUsersAsync(new[] { targetUser.Id }, "New document request",
                        $"{institutionName} has requested {documents} from you.{purpose}{deadline}",
                        "RequestReceived", link: RequestsPage, sendEmail: true);
                }
                else if (targetDepartment != null)
                {
                    var adminIds = await notifications.GetUserIdsInRoleAsync("Department Admin", targetDepartment.DepartmentId);
                    await notifications.NotifyUsersAsync(adminIds, "New department document request",
                        $"{institutionName} has requested {documents} from {targetDepartment.DepartmentName}. Assign it to the right person or deny it.{purpose}{deadline}",
                        "RequestReceived", link: RequestsPage, sendEmail: true);
                }
            });

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
                    .ThenInclude(u => u!.Profile)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Include(r => r.AccessToken)
                .Where(r => r.InstitutionId == institutionId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Every request the institution made, so decided and cancelled ones stay visible too.
            // The document checklist only matters while a request is still waiting for a decision.
            var checklists = new Dictionary<int, (bool IsComplete, int MissingCount, object RequestedDocumentStatuses)>();
            foreach (var request in requests.Where(r => r.Status == "Pending" || r.Status == "Department_Pending"))
            {
                checklists[request.EnquiryRequestId] = await ComputeInstitutionRequestChecklistAsync(request);
            }

            return Ok(requests.Select(r => new
            {
                r.EnquiryRequestId,
                r.InstitutionId,
                InstitutionName = r.Institution.InstitutionName,
                r.RequestType,
                Recipient = new { Type = r.RequestType == "Department" ? "Department" : "Individual", Name = RecipientDisplayName(r) },
                r.Status,
                r.PurposeNote,
                r.RequestDate,
                r.ReferenceNumber,
                r.SubmissionDeadline,
                r.RespondedAt,
                ResponseNote = r.UserResponseNote,
                AccessExpiresAt = r.AccessToken != null && !r.AccessToken.IsRevoked ? r.AccessToken.ExpiryTimeStamp : (DateTimeOffset?)null,
                r.ExtensionStatus,
                r.ExtensionRequestedUntil,
                r.ExtensionResponseNote,
                Documents = r.RequestedDocumentTypes.Select(d => new
                {
                    d.DocumentTypeId,
                    DocumentTypeName = d.DocumentType.TypeName,
                    d.isMandatory
                }),
                IsComplete = checklists.TryGetValue(r.EnquiryRequestId, out var checklist) ? checklist.IsComplete : (bool?)null,
                MissingCount = checklists.TryGetValue(r.EnquiryRequestId, out var missing) ? missing.MissingCount : (int?)null,
                RequestedDocumentStatuses = checklists.TryGetValue(r.EnquiryRequestId, out var statuses) ? statuses.RequestedDocumentStatuses : null
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
                .Include(r => r.TargetUser).ThenInclude(u => u!.Profile)
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
                    : request.TargetUser?.Profile == null ? request.TargetUserId : $"{request.TargetUser.Profile!.FirstName} {request.TargetUser.Profile.LastName}".Trim(),
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

            // The same rule as when the request was created: a department is only asked for the types it handles.
            if (request.RequestType == "Department" && request.TargetDepartmentId.HasValue)
            {
                var allowed = await GetDepartmentDocumentTypeIdsAsync(request.TargetDepartmentId.Value);
                var notAllowed = documentTypeIds.Except(allowed).ToList();
                if (notAllowed.Count > 0)
                    return BadRequest(new
                    {
                        error = "One or more requested document types do not belong to the target department's required documents.",
                        invalidDocumentTypeIds = notAllowed,
                        allowedDocumentTypeIds = allowed
                    });
            }

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

            await TryNotifyAsync(async notifications =>
            {
                var recipients = await GetRequestRecipientsAsync(request, notifications);
                var institutionName = await GetInstitutionNameAsync(request.InstitutionId);
                await notifications.NotifyUsersAsync(recipients, "Document request updated",
                    $"{institutionName} changed {DescribeRequest(request)}. Check the documents it now asks for.",
                    "RequestUpdated", link: RequestsPage);
            });

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

            await TryNotifyAsync(async notifications =>
            {
                var recipients = await GetRequestRecipientsAsync(request, notifications);
                var institutionName = await GetInstitutionNameAsync(request.InstitutionId);
                await notifications.NotifyUsersAsync(recipients, "Document request cancelled",
                    $"{institutionName} cancelled {DescribeRequest(request)}. No action is needed.",
                    "RequestCancelled", link: RequestsPage);
            });

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
                    .ThenInclude(u => u!.Profile)
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
                Recipient = new { Type = request.RequestType == "Department" ? "Department" : "Individual", Name = RecipientDisplayName(request) },
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
                var documentOwnerUserIds = _context.UserRoles
                    .Where(userRole => userRole.RoleId == docRole.Id)
                    .Select(userRole => userRole.UserId);

                members = await _context.Users
                    .Include(u => u.Profile)
                    .Where(u => documentOwnerUserIds.Contains(u.Id))
                    .Select(u => new
                    {
                        userId = u.Id,
                        userName = (string?)null,
                        // Institutions see names only: usernames are often email addresses.
                        displayName = u.Profile != null && (!string.IsNullOrWhiteSpace(u.Profile.FirstName) || !string.IsNullOrWhiteSpace(u.Profile.LastName))
                            ? (u.Profile.FirstName + " " + u.Profile.LastName).Trim()
                            : "Unnamed document owner"
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

                // Only offer what the department accepts: creating the request rejects any other type.
                // Mandatory flags and notes come from the company requirements where they exist.
                var departmentTypeIds = await GetDepartmentDocumentTypeIdsAsync(departmentId);
                var companyRequirements = await _context.RequiredDocuments
                    .AsNoTracking()
                    .Where(rd => rd.EntityTypeId == 3 && departmentTypeIds.Contains(rd.DocumentTypeId))
                    .Select(rd => new { rd.DocumentTypeId, rd.IsMandatory, rd.Description })
                    .ToListAsync();

                var departmentDocumentTypes = (await _context.DocumentTypes
                        .AsNoTracking()
                        .Where(dt => departmentTypeIds.Contains(dt.DocumentTypeId))
                        .Select(dt => new { dt.DocumentTypeId, dt.TypeName, dt.Description })
                        .ToListAsync())
                    .Select(dt =>
                    {
                        var requirement = companyRequirements.FirstOrDefault(rd => rd.DocumentTypeId == dt.DocumentTypeId);
                        return new
                        {
                            documentTypeId = dt.DocumentTypeId,
                            typeName = dt.TypeName,
                            description = dt.Description,
                            isMandatory = requirement?.IsMandatory ?? false,
                            requirementNote = requirement?.Description ?? dt.Description
                        };
                    })
                    .OrderBy(dt => dt.isMandatory ? 0 : 1)
                    .ThenBy(dt => dt.typeName)
                    .ToList();

                if (departmentDocumentTypes.Count == 0)
                {
                    return Ok(new
                    {
                        documentTypes = departmentDocumentTypes,
                        warning = $"{department.DepartmentName} has no document types set up yet, so it can't receive requests. Contact DocuVault support."
                    });
                }

                return Ok(new { documentTypes = departmentDocumentTypes });
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
                    .ThenInclude(u => u!.Profile)
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

            await TryNotifyAsync(async notifications =>
            {
                var institutionName = await GetInstitutionNameAsync(request.InstitutionId);
                var note = string.IsNullOrWhiteSpace(dto.AdminNote) ? string.Empty : $" Note from your department admin: {dto.AdminNote.Trim()}";
                await notifications.NotifyUsersAsync(new[] { targetUser.Id }, "Document request assigned to you",
                    $"Your department admin assigned you {institutionName}'s {DescribeRequest(request)}. Review it and approve or deny it.{note}",
                    "RequestAssigned", link: RequestsPage, sendEmail: true);
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

            await _complianceService.CheckUserComplianceAsync(request.TargetUserId!);

            var uploadedDocumentIds = approvedDocuments.Select(document => document.DocumentId).ToList();
            var latestChecks = await _context.DocumentComplianceChecks
                .AsNoTracking()
                .Where(check => check.DocumentId.HasValue && uploadedDocumentIds.Contains(check.DocumentId.Value))
                .OrderByDescending(check => check.CheckedAt)
                .ThenByDescending(check => check.CheckId)
                .ToListAsync();

            var latestCheckByDocument = latestChecks
                .GroupBy(check => check.DocumentId!.Value)
                .ToDictionary(group => group.Key, group => group.First());
            var documentsAwaitingReview = approvedDocuments
                .Where(document => !latestCheckByDocument.TryGetValue(document.DocumentId, out var check)
                    || !check.IsManuallyApproved
                    || !string.Equals(check.CheckStatus, "Compliant", StringComparison.OrdinalIgnoreCase))
                .Select(document => new { document.DocumentId, document.FileName })
                .ToList();

            if (documentsAwaitingReview.Count > 0)
            {
                return Conflict(new
                {
                    error = "A Compliance Officer must approve every requested document before this request can be accepted.",
                    documents = documentsAwaitingReview
                });
            }

            var expiry = DateTimeOffset.UtcNow.AddHours(await _settings.GetAsync(SystemSettingDefinitions.DocumentAccessLinkExpiryHours));
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

            await TryNotifyAsync(notifications =>
            {
                var count = approvedDocuments.Count;
                notifications.EmailExternal(request.RequesterEmail, "Your document request was approved",
                    $"Your {DescribeRequest(request)} was approved. {count} document{(count == 1 ? " is" : "s are")} now available in the DocuVault institution portal until {expiry:d MMM yyyy}.");
                return Task.CompletedTask;
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

            await TryNotifyAsync(notifications =>
            {
                var reason = string.IsNullOrWhiteSpace(request.UserResponseNote) ? string.Empty : $" Reason: {request.UserResponseNote.Trim()}";
                notifications.EmailExternal(request.RequesterEmail, "Your document request was denied",
                    $"Your {DescribeRequest(request)} was denied.{reason}");
                return Task.CompletedTask;
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
            [FromQuery] string token,
            [FromQuery] bool inline = false)
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
                await LogInstitutionDownloadAsync(sessionToken.InstitutionId, "DocumentAccessApprovals", approval.ApprovalId, document.FileName, approval.EnquiryRequestId, inline);
                return InstitutionFile(fileBytes, document.FileName, inline);
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
            await LogInstitutionDownloadAsync(request.InstitutionId, "InstitutionEnquiryRequests", request.EnquiryRequestId, documentByToken.FileName, request.EnquiryRequestId, inline);
            return InstitutionFile(fileBytesToken, documentByToken.FileName, inline);
        }

        /// <summary>
        /// A download, or with inline the file shown in the browser (no copy saved on the institution's computer).
        /// Only PDFs and images can be shown inline; anything else is still downloaded.
        /// </summary>
        private IActionResult InstitutionFile(byte[] bytes, string fileName, bool inline)
        {
            var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => null
            };
            if (!inline || contentType == null)
                return File(bytes, "application/octet-stream", fileName);

            Response.Headers["Content-Disposition"] = "inline";
            Response.Headers["Cache-Control"] = "no-store";
            return File(bytes, contentType);
        }

        /// <summary>
        /// The institution asks for more time on access it was given, instead of making a new request.
        /// The owner who approved the request decides.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("institution-access/requests/{requestId:int}/extension")]
        public async Task<IActionResult> RequestAccessExtension([FromRoute] int requestId, [FromQuery] string token, [FromBody] RequestAccessExtensionDto dto)
        {
            var sessionToken = string.IsNullOrWhiteSpace(token) ? null : await _context.InstitutionSessionTokens
                .FirstOrDefaultAsync(st => st.TokenString == token && !st.IsRevoked && st.ExpiresAt > DateTime.UtcNow);
            if (sessionToken == null)
                return Unauthorized(new { error = "Your session has ended. Please sign in again." });

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.AccessToken)
                .Include(r => r.Institution)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId && r.InstitutionId == sessionToken.InstitutionId);
            if (request == null)
                return NotFound(new { error = "Request not found." });
            if (request.Status != "Approved" || request.AccessToken == null || request.AccessToken.IsRevoked)
                return Conflict(new { error = "More time can only be asked for on a request whose access is still approved." });
            if (request.ExtensionStatus == "Pending")
                return Conflict(new { error = "You have already asked for more time on this request. The owner hasn't answered yet." });
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(new { error = "Say why you need the documents for longer." });

            var maxDays = await _settings.GetAsync(SystemSettingDefinitions.MaxAccessExtensionDays);
            if (dto.Days < 1 || dto.Days > maxDays)
                return BadRequest(new { error = $"You can ask for between 1 and {maxDays} extra days." });

            // Counted from when access ends now, or from today if it has already ended.
            var from = request.AccessToken.ExpiryTimeStamp > DateTimeOffset.UtcNow ? request.AccessToken.ExpiryTimeStamp : DateTimeOffset.UtcNow;
            request.ExtensionRequestedUntil = from.AddDays(dto.Days);
            request.ExtensionReason = dto.Reason.Trim();
            request.ExtensionStatus = "Pending";
            request.ExtensionRequestedAt = DateTime.UtcNow;
            request.ExtensionRespondedAt = null;
            request.ExtensionResponseNote = null;
            await _context.SaveChangesAsync();

            await TryAuditAsync(new AuditLog
            {
                InstitutionId = request.InstitutionId,
                ActionCode = "ACCESS_EXTENSION_REQUESTED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"{request.Institution.InstitutionName} asked for {dto.Days} more day{(dto.Days == 1 ? "" : "s")} on {DescribeRequest(request)}. Reason: {request.ExtensionReason}",
                TableAffected = "InstitutionEnquiryRequests",
                RecordID = request.EnquiryRequestId
            });

            var approverId = request.ApprovedByUserId ?? request.TargetUserId;
            if (!string.IsNullOrWhiteSpace(approverId))
            {
                await TryNotifyAsync(notifications => notifications.NotifyUsersAsync(new[] { approverId },
                    "An institution asked for more time",
                    $"{request.Institution.InstitutionName} asked to keep access to the documents for {DescribeRequest(request)} until {request.ExtensionRequestedUntil.Value.ToOffset(TimeSpan.FromHours(2)):d MMM yyyy}. Reason: {request.ExtensionReason}",
                    "AccessExtension", link: RequestsPage, sendEmail: true));
            }

            return Ok(new { request.EnquiryRequestId, request.ExtensionStatus, request.ExtensionRequestedUntil });
        }

        /// <summary>Extension requests waiting for the signed-in owner's answer.</summary>
        [HttpGet("document-access-requests/extensions")]
        public async Task<IActionResult> GetPendingExtensions()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var requests = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.Institution)
                .Include(r => r.AccessToken)
                .Where(r => r.ExtensionStatus == "Pending" && r.Status == "Approved"
                    && (r.ApprovedByUserId == currentUser.Id || (r.ApprovedByUserId == null && r.TargetUserId == currentUser.Id)))
                .OrderBy(r => r.ExtensionRequestedAt)
                .ToListAsync();

            return Ok(requests.Select(r => new
            {
                r.EnquiryRequestId,
                r.ReferenceNumber,
                InstitutionName = r.Institution.InstitutionName,
                r.PurposeNote,
                CurrentAccessEndsAt = r.AccessToken?.ExpiryTimeStamp,
                r.ExtensionRequestedUntil,
                r.ExtensionReason,
                r.ExtensionRequestedAt
            }));
        }

        [HttpPost("document-access-requests/{requestId:int}/extension/approve")]
        public Task<IActionResult> ApproveExtension([FromRoute] int requestId, [FromBody] FollowUpNoteDto? dto = null) =>
            DecideExtensionAsync(requestId, approve: true, dto?.Note);

        [HttpPost("document-access-requests/{requestId:int}/extension/deny")]
        public Task<IActionResult> DenyExtension([FromRoute] int requestId, [FromBody] FollowUpNoteDto? dto = null) =>
            DecideExtensionAsync(requestId, approve: false, dto?.Note);

        private async Task<IActionResult> DecideExtensionAsync(int requestId, bool approve, string? note)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.AccessToken)
                .Include(r => r.Institution)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);
            if (request == null) return NotFound(new { error = "Request not found." });

            var decider = request.ApprovedByUserId ?? request.TargetUserId;
            if (decider != currentUser.Id) return Forbid();
            if (request.ExtensionStatus != "Pending" || request.ExtensionRequestedUntil == null)
                return Conflict(new { error = "There is no request for more time waiting on this request." });
            if (approve && (request.Status != "Approved" || request.AccessToken == null || request.AccessToken.IsRevoked))
                return Conflict(new { error = "Access on this request was withdrawn, so it can't be extended." });

            request.ExtensionStatus = approve ? "Approved" : "Denied";
            request.ExtensionRespondedAt = DateTime.UtcNow;
            request.ExtensionResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            if (approve)
            {
                var until = request.ExtensionRequestedUntil.Value;
                request.AccessToken!.ExpiryTimeStamp = until;
                request.AccessToken.ExpiryReminderSentAt = null; // warn again before the new end
                var approvals = await _context.DocumentAccessApprovals
                    .Where(a => a.EnquiryRequestId == requestId && !a.IsRevoked)
                    .ToListAsync();
                foreach (var approval in approvals)
                    approval.ExpiresAt = until.UtcDateTime;
            }

            await _context.SaveChangesAsync();

            var endsAt = request.ExtensionRequestedUntil.Value.ToOffset(TimeSpan.FromHours(2));
            await TryAuditAsync(new AuditLog
            {
                UserId = currentUser.Id,
                ActionCode = approve ? "ACCESS_EXTENSION_APPROVED" : "ACCESS_EXTENSION_DENIED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = approve
                    ? $"Access for {DescribeRequest(request)} extended to {endsAt:yyyy-MM-dd HH:mm} by {currentUser.UserName}."
                    : $"More time on {DescribeRequest(request)} declined by {currentUser.UserName}.",
                TableAffected = "InstitutionEnquiryRequests",
                RecordID = request.EnquiryRequestId
            });

            await TryNotifyAsync(notifications =>
            {
                notifications.EmailExternal(request.RequesterEmail,
                    approve ? "Your access was extended" : "Your request for more time was declined",
                    approve
                        ? $"Your access to the documents for {DescribeRequest(request)} now ends on {endsAt:d MMM yyyy 'at' HH:mm} (SAST)."
                        : $"The owner declined your request for more time on {DescribeRequest(request)}." + (request.ExtensionResponseNote == null ? string.Empty : $" Reason: {request.ExtensionResponseNote}"));
                return Task.CompletedTask;
            });

            return Ok(new { request.EnquiryRequestId, request.ExtensionStatus, AccessEndsAt = request.AccessToken?.ExpiryTimeStamp });
        }

        private async Task TryAuditAsync(AuditLog log)
        {
            try { await _auditLogService.CreateAuditLogAsync(log); }
            catch { /* The action itself matters more than its audit entry. */ }
        }

        /// <summary>Records an institution's download or view in the audit trail (the reports read it). Never blocks the document.</summary>
        private async Task LogInstitutionDownloadAsync(int institutionId, string table, int recordId, string fileName, int requestId, bool viewed = false)
        {
            try
            {
                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    InstitutionId = institutionId,
                    ActionCode = viewed ? "INSTITUTION_DOCUMENT_VIEWED" : "INSTITUTION_DOCUMENT_DOWNLOADED",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = $"{(viewed ? "Viewed" : "Downloaded")} \"{fileName}\" under request #{requestId}.",
                    TableAffected = table,
                    RecordID = recordId
                });
            }
            catch
            {
                // Audit failures must not stop the institution getting a document it was approved for.
            }
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

            await TryNotifyAsync(async notifications =>
            {
                var document = await _context.Documents
                    .AsNoTracking()
                    .Where(d => d.DocumentId == documentId)
                    .Select(d => new { d.UserId, d.FileName, OwnerDepartmentId = d.User.DepartmentId })
                    .FirstOrDefaultAsync();
                if (document == null) return;

                var institutionName = await GetInstitutionNameAsync(sessionToken.InstitutionId);
                var message = $"{institutionName} flagged the document \"{document.FileName}\". Reason: {flag.FlagReason}";
                await notifications.NotifyUsersAsync(new[] { document.UserId }, "Document flagged by an institution",
                    message, "DocumentFlagged", documentId, link: "/my-documents", sendEmail: true);

                if (document.OwnerDepartmentId != null)
                {
                    var adminIds = (await notifications.GetUserIdsInRoleAsync("Department Admin", document.OwnerDepartmentId))
                        .Where(id => id != document.UserId);
                    await notifications.NotifyUsersAsync(adminIds, "Document flagged by an institution",
                        message, "DocumentFlagged", documentId, link: "/dashboard/department", sendEmail: true);
                }
            });

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
                            .ThenInclude(u => u!.Profile)
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
                                ? ((!string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile!.FirstName) || !string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName))
                                    ? (daa.InstitutionEnquiryRequest.TargetUser.Profile.FirstName + " " + daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName).Trim()
                                    : (daa.InstitutionEnquiryRequest.TargetUser.UserName ?? daa.InstitutionEnquiryRequest.TargetUser.Id))
                                : (daa.InstitutionEnquiryRequest.TargetUserId ?? "-")),
                        daa.DocumentId,
                        DocumentName = daa.Document.FileName,
                        DocumentTypeName = daa.Document.DocumentType.TypeName,
                        ApprovedAt = daa.ApprovedAt,
                        ExpiresAt = daa.ExpiresAt,
                        daa.InstitutionEnquiryRequest.ExtensionStatus,
                        daa.InstitutionEnquiryRequest.ExtensionRequestedUntil
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
                            .ThenInclude(u => u!.Profile)
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
                            ? ((!string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile!.FirstName) || !string.IsNullOrWhiteSpace(daa.InstitutionEnquiryRequest.TargetUser.Profile.LastName))
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

            var requests = await _context.InstitutionEnquiryRequests
                .AsNoTracking()
                .Include(r => r.TargetDepartment)
                .Include(r => r.TargetUser)
                    .ThenInclude(u => u!.Profile)
                .Where(r => r.InstitutionId == sessionToken.InstitutionId)
                .ToListAsync();
            var requestById = requests.ToDictionary(r => r.EnquiryRequestId);

            object Item(InstitutionEnquiryRequest r, string status, string message, DateTime? at) => new
            {
                r.EnquiryRequestId,
                Status = status,
                r.RequestType,
                RecipientName = RecipientDisplayName(r),
                Message = message,
                Timestamp = at
            };

            var feed = new List<(DateTime At, object Item)>();
            foreach (var r in requests.Where(r => (r.Status == "Approved" || r.Status == "Denied") && r.RespondedAt != null))
            {
                var message = r.Status == "Approved"
                    ? "Your documents were approved and are available for download."
                    : string.IsNullOrWhiteSpace(r.UserResponseNote) ? "Your request was denied." : "Your request was denied. Reason: " + r.UserResponseNote;
                feed.Add((r.RespondedAt!.Value, Item(r, r.Status, message, r.RespondedAt)));
            }

            foreach (var r in requests.Where(r => r.ExtensionRespondedAt != null && r.ExtensionStatus is "Approved" or "Denied"))
            {
                var message = r.ExtensionStatus == "Approved"
                    ? $"Your access was extended to {r.ExtensionRequestedUntil!.Value.ToOffset(TimeSpan.FromHours(2)):d MMM yyyy, HH:mm}."
                    : "Your request for more time was declined." + (string.IsNullOrWhiteSpace(r.ExtensionResponseNote) ? string.Empty : " Reason: " + r.ExtensionResponseNote);
                feed.Add((r.ExtensionRespondedAt!.Value, Item(r, $"Extension {r.ExtensionStatus!.ToLowerInvariant()}", message, r.ExtensionRespondedAt)));
            }

            var requestIds = requestById.Keys.ToList();
            var resolvedFlags = await _context.EnquiryFlags
                .AsNoTracking()
                .Where(f => f.IsResolved && f.ResolvedAt != null && requestIds.Contains(f.EnquiryId))
                .Select(f => new { f.EnquiryId, f.DocumentId, f.ResolvedAt, f.ResolutionNote, f.FlagReason })
                .ToListAsync();
            var flaggedFileNames = await _context.Documents
                .AsNoTracking()
                .Where(d => resolvedFlags.Select(f => f.DocumentId).Contains(d.DocumentId))
                .ToDictionaryAsync(d => d.DocumentId, d => d.FileName);
            foreach (var flag in resolvedFlags)
            {
                var file = flaggedFileNames.GetValueOrDefault(flag.DocumentId, "a document");
                var message = $"The owner dealt with your flag on \"{file}\"." + (string.IsNullOrWhiteSpace(flag.ResolutionNote) ? string.Empty : " " + flag.ResolutionNote);
                var at = flag.ResolvedAt!.Value.UtcDateTime;
                feed.Add((at, Item(requestById[flag.EnquiryId], "Flag resolved", message, at)));
            }

            var notifications = feed.OrderByDescending(entry => entry.At).Take(15).Select(entry => entry.Item).ToList();
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
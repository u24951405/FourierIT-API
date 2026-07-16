using System.Security.Cryptography;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public DocumentAccessRequestsController(
            AppDbContext context,
            UserManager<User> userManager,
            IDocumentService documentService)
        {
            _context = context;
            _userManager = userManager;
            _documentService = documentService;
        }

        [HttpPost("institutions/{institutionId:int}/document-access-requests")]
        public async Task<IActionResult> CreateRequest(
            [FromRoute] int institutionId,
            [FromBody] InstitutionDocumentRequestDto dto)
        {
            var actor = await _userManager.GetUserAsync(User);
            if (actor == null)
                return Unauthorized();

            if (dto == null || string.IsNullOrWhiteSpace(dto.TargetUserId))
                return BadRequest(new { error = "Target user is required." });

            var isMember = await _context.Institutions
                .Include(i => i.InstitutionMembers)
                .Where(i => i.InstitutionId == institutionId)
                .SelectMany(i => i.InstitutionMembers)
                .AnyAsync(im => im.UserId == actor.Id);

            if (!isMember)
                return Forbid();

            var targetUser = await ResolveUserAsync(dto.TargetUserId);
            if (targetUser == null)
                return NotFound(new { error = "Target user was not found." });

            var requestedDocumentTypeIds = dto.RequestedDocuments
                .Select(r => r.DocumentTypeId)
                .Distinct()
                .ToList();

            if (!requestedDocumentTypeIds.Any())
                return BadRequest(new { error = "At least one document type must be requested." });

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
                    error = "The target user does not currently have every requested document type.",
                    missingDocumentTypeIds = missingDocumentTypes
                });
            }

            var defaultRule = await EnsureDefaultFicaRuleAsync();

            var request = new InstitutionEnquiryRequest
            {
                InstitutionId = institutionId,
                TargetUserId = targetUser.Id,
                PurposeNote = dto.PurposeNote?.Trim() ?? string.Empty,
                Status = "Pending",
                RequestDate = DateTimeOffset.UtcNow
            };

            _context.InstitutionEnquiryRequests.Add(request);
            await _context.SaveChangesAsync();

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
                request.TargetUserId,
                request.Status,
                request.RequestDate,
                RequestedDocumentTypeIds = requestedDocumentTypeIds
            });
        }

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

        [HttpPost("document-access-requests/{requestId:int}/approve")]
        public async Task<IActionResult> Approve([FromRoute] int requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.Institution)
                .Include(r => r.TargetUser)
                .Include(r => r.RequestedDocumentTypes)
                    .ThenInclude(rdt => rdt.DocumentType)
                .Include(r => r.AccessToken)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            if (request.TargetUserId != currentUser.Id)
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
            request.UserResponseNote = null;

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
        public async Task<IActionResult> Deny([FromRoute] int requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var request = await _context.InstitutionEnquiryRequests
                .Include(r => r.AccessToken)
                .FirstOrDefaultAsync(r => r.EnquiryRequestId == requestId);

            if (request == null)
                return NotFound(new { error = "Request not found." });

            if (request.TargetUserId != currentUser.Id)
                return Forbid();

            request.Status = "Denied";
            request.RespondedAt = DateTime.UtcNow;

            if (request.AccessToken != null)
                request.AccessToken.IsRevoked = true;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Request denied." });
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
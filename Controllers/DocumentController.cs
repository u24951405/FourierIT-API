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

        public DocumentController(IDocumentService documentService, IDocumentRepository documentRepository, UserManager<User> userManager, AppDbContext context)
        {
            _documentService = documentService;
            _documentRepository = documentRepository;
            _userManager = userManager;
            _context = context;
        }

        //Role helper method
        private async Task<bool> IsAdminOrViewerRole(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Department Admin") || roles.Contains("Compliance Officer") || roles.Contains("Stakeholder");
        }

        [HttpGet("api/users/me/documents/required")]
        public async Task<IActionResult> GetRequiredDocumentsStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            //Get user's entity type
            var userWithEntity = await _context.Users
                .Include(u => u.EntityType)
                .ThenInclude(et => et.RequiredDocuments)
                .ThenInclude(rd => rd.DocumentType)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (userWithEntity?.EntityType == null)
                return BadRequest(new { error = "User has not selected an entity type." });

            var entityType = userWithEntity.EntityType;
            var requiredDocs = entityType.RequiredDocuments;

            //Get user's uploaded documents grouped by type
            var userDocs = await _context.Documents
                .Where(d => d.UserId == user.Id && d.CurrentStatus != "Deleted")
                .GroupBy(d => d.DocumentTypeId)
                .Select(g => new { DocumentTypeId = g.Key, count = g.Count() })
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

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("No file provided");

            using var ms = new MemoryStream();
            await dto.File.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            var document = await _documentService.UploadDocumentAsync(user.Id, dto.File.FileName, fileBytes);

            document.DocumentTypeId = dto.DocumentTypeId;
            document.IsCertified = dto.IsCertified;
            document.CurrentStatus = "Active";

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

            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, ToResponseDto(document));
        }

        [HttpGet]
        public async Task<IActionResult> GetMyDocuments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var docs = await _documentService.GetAccessibleDocumentAsync(user.Id);
            return Ok(docs.Select(ToResponseDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();


            //Allow if: Owner, shared with user or admin/viewer role
            var isAdminViewer = await IsAdminOrViewerRole(user);
            if (doc.UserId != user.Id && !doc.SharedWith.Any(s => s.GrantedToUserId == user.Id) && !isAdminViewer)
                return Forbid();

            return Ok(ToResponseDto(doc));
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var doc = await _documentRepository.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound();

                //check access: owner, shared, or admin/viewer
                var isAdminViewer = await IsAdminOrViewerRole(user);
                if (doc.UserId != user.Id && !doc.SharedWith.Any(s => s.GrantedToUserId == user.Id) && !isAdminViewer)
                    throw new UnauthorizedAccessException("No access to this document");

                var fileBytes = await _documentService.DownloadDocumentAsync(id, doc.User.Id); // decrypt using owner's key context

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

                return File(fileBytes, "application/octet-stream", doc.FileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] UploadDocumentDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            if (doc.UserId != user.Id) return Forbid();

            if (dto.File != null && dto.File.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.File.CopyToAsync(ms);
                var fileBytes = ms.ToArray();

                var updated = await _documentService.UploadDocumentAsync(user.Id, dto.File.FileName, fileBytes);
                doc.DocumentBlob = updated.DocumentBlob;
                doc.FileName = dto.File.FileName;
                doc.FileSizeBytes = fileBytes.Length;
            }

            doc.DocumentTypeId = dto.DocumentTypeId;
            doc.IsCertified = dto.IsCertified;
            doc.LastModifiedDate = DateTime.UtcNow;

            await _documentRepository.UpdateDocumentAsync(doc);
            return Ok(ToResponseDto(doc));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _documentRepository.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            if (doc.UserId != user.Id) return Forbid();

            await _documentRepository.DeleteDocumentAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/share")]
        public async Task<IActionResult> Share(int id, [FromBody] ShareDocumentDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var success = await _documentService.ShareDocumentAsync(id, dto.GrantToUserId, dto.AccessLevel, user.Id);
            if (!success) return Forbid();

            return Ok(new { message = "Document shared successfully." });
        }

        //Admin/compliance/stakeholder endpoint to view all users and their documents
        [HttpGet("admin/all-users-documents")]
        [Authorize(Roles = "Department Admin, Compliance Officer, Stakeholder")]
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
            LastModifiedDate = doc.LastModifiedDate,
            DocumentTypeName = doc.DocumentType?.TypeName ?? string.Empty
        };

        [HttpGet("api/users/me/document-types")]
        public async Task<IActionResult> GetDocumentTypesForMyEntity()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

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

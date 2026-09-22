using FourierIT_API.Data;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Repositories
{
    public class DocumentHierarchyRepository
    {
        private readonly AppDbContext _context;

        public DocumentHierarchyRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets full hierarchy for Super Admin (all entity types, document types, and documents)
        /// </summary>
        public async Task<List<EntityType>> GetFullHierarchyAsync()
        {
            return await _context.EntityTypes
                .Include(et => et.RequiredDocuments)
                    .ThenInclude(rd => rd.DocumentType)
                .OrderBy(et => et.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets hierarchy for a specific user, filtered by their document access permissions
        /// </summary>
        public async Task<List<EntityType>> GetHierarchyForUserAsync(string userId)
        {
            var userDocuments = await _context.Documents
                .Where(d => d.UserId == userId)
                .Select(d => d.DocumentTypeId)
                .Distinct()
                .ToListAsync();

            // Also get documents shared with the user
            var sharedDocuments = await _context.DocumentAccesses
                .Where(da => da.GrantedToUserId == userId)
                .Include(da => da.Document)
                .Select(da => da.Document.DocumentTypeId)
                .Distinct()
                .ToListAsync();

            // Combine both lists
            var accessibleDocumentTypeIds = userDocuments.Union(sharedDocuments).ToList();

            if (!accessibleDocumentTypeIds.Any())
            {
                return new List<EntityType>();
            }

            // Get entity types that have the accessible document types
            var requiredDocs = await _context.RequiredDocuments
                .Where(rd => accessibleDocumentTypeIds.Contains(rd.DocumentTypeId))
                .Select(rd => rd.EntityTypeId)
                .Distinct()
                .ToListAsync();

            if (!requiredDocs.Any())
            {
                return new List<EntityType>();
            }

            // Build the filtered hierarchy
            return await _context.EntityTypes
                .Where(et => requiredDocs.Contains(et.EntityTypeId))
                .Include(et => et.RequiredDocuments)
                    .ThenInclude(rd => rd.DocumentType)
                        .ThenInclude(dt => dt.Documents.Where(d =>
                            d.UserId == userId ||
                            d.SharedWith.Any(da => da.GrantedToUserId == userId)))
                .OrderBy(et => et.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets document types for a specific entity type
        /// </summary>
        public async Task<List<DocumentType>> GetDocumentTypesByEntityTypeAsync(int entityTypeId)
        {
            return await _context.RequiredDocuments
                .Where(rd => rd.EntityTypeId == entityTypeId)
                .Include(rd => rd.DocumentType)
                .Select(rd => rd.DocumentType)
                .OrderBy(dt => dt.TypeName)
                .ToListAsync();
        }

        /// <summary>
        /// Gets documents for a specific document type
        /// </summary>
        public async Task<List<Document>> GetDocumentsByTypeAsync(int documentTypeId)
        {
            return await _context.Documents
                .Where(d => d.DocumentTypeId == documentTypeId)
                .Include(d => d.DocumentType)
                .Include(d => d.SharedWith)
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Searches for documents matching the query string
        /// </summary>
        public async Task<List<Document>> SearchDocumentsAsync(string query)
        {
            var lowerQuery = query.ToLower();

            return await _context.Documents
                .Where(d =>
                    d.FileName.ToLower().Contains(lowerQuery) ||
                    d.DocumentType.TypeName.ToLower().Contains(lowerQuery) ||
                    d.CurrentStatus.ToLower().Contains(lowerQuery))
                .Include(d => d.DocumentType)
                .Include(d => d.SharedWith)
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Searches for documents for a specific user with their permissions
        /// </summary>
        public async Task<List<Document>> SearchDocumentsForUserAsync(string userId, string query)
        {
            var lowerQuery = query.ToLower();

            return await _context.Documents
                .Where(d =>
                    (d.UserId == userId || d.SharedWith.Any(da => da.GrantedToUserId == userId)) &&
                    (d.FileName.ToLower().Contains(lowerQuery) ||
                     d.DocumentType.TypeName.ToLower().Contains(lowerQuery) ||
                     d.CurrentStatus.ToLower().Contains(lowerQuery)))
                .Include(d => d.DocumentType)
                .Include(d => d.SharedWith)
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Checks if a user has access to view a document
        /// </summary>
        public async Task<bool> UserHasAccessToDocumentAsync(string userId, int documentId)
        {
            return await _context.Documents
                .Where(d => d.DocumentId == documentId)
                .AnyAsync(d =>
                    d.UserId == userId ||
                    d.SharedWith.Any(da => da.GrantedToUserId == userId));
        }

        /// <summary>
        /// Gets user's permission level for a document
        /// </summary>
        public async Task<DocumentAccessDto?> GetUserDocumentPermissionsAsync(string userId, int documentId)
        {
            var document = await _context.Documents
                .Include(d => d.SharedWith)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (document == null)
                return null;

            // Owner has all permissions
            if (document.UserId == userId)
            {
                return new DocumentAccessDto
                {
                    DocumentId = documentId,
                    CanView = true,
                    CanDownload = true,
                    CanEdit = true,
                    CanDelete = true,
                    CanShare = true
                };
            }

            // Check if shared with user
            var access = document.SharedWith.FirstOrDefault(da => da.GrantedToUserId == userId);
            if (access != null)
            {
                return new DocumentAccessDto
                {
                    DocumentId = documentId,
                    CanView = true,
                    CanDownload = access.AccessLevel == AccessLevel.Download || access.AccessLevel == AccessLevel.Edit || access.AccessLevel == AccessLevel.Share,
                    CanEdit = access.AccessLevel == AccessLevel.Edit || access.AccessLevel == AccessLevel.Share,
                    CanDelete = access.AccessLevel == AccessLevel.Share,
                    CanShare = access.AccessLevel == AccessLevel.Share
                };
            }

            return null;
        }
    }

    public class DocumentAccessDto
    {
        public int DocumentId { get; set; }
        public bool CanView { get; set; }
        public bool CanDownload { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanShare { get; set; }
    }
}

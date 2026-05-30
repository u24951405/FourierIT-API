using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Security;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Service
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repo;
        private readonly IEncryptionService _encryptionService;
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;

        public DocumentService(IDocumentRepository repo, IEncryptionService encryptionService, IConfiguration config, AppDbContext context)
        {
            _repo = repo;
            _encryptionService = encryptionService;
            _config = config;
            _context = context;
        }

        public async Task<byte[]> DownloadDocumentAsync(int documentId, string userId)
        {
            var doc = await _repo.GetDocumentByIdAsync(documentId);
            if (doc == null) throw new UnauthorizedAccessException("Document not found");

            //Check access: owner or has document access
            if (doc.UserId != userId && !doc.SharedWith.Any(d => d.GrantedToUserId == userId))
                throw new UnauthorizedAccessException("No access to this document");

            var encryptionKey = _config["Vault:EncryptionKey"];
            return _encryptionService.DecryptData(doc.DocumentBlob.FileData, encryptionKey);
        }

        public async Task<List<Document>> GetAccessibleDocumentAsync(string userId)
        {
            var ownedDocs = await _repo.GetUserDocumentAsync(userId);
            var sharedDocs = await _context.DocumentAccesses
                .Where(da => da.GrantedToUserId == userId)
                .Include(da => da.Document).ThenInclude(d => d.DocumentBlob)
                .Select(da => da.Document)
                .ToListAsync();

            return ownedDocs.Concat(sharedDocs).Distinct().ToList();
        }

        public async Task<bool> ShareDocumentAsync(int documentId, string grantToUserId, AccessLevel accessLevel, string ownerUserId)
        {
            var doc = await _repo.GetDocumentByIdAsync(documentId);
            if (doc?.UserId != ownerUserId) return false;

            var access = new DocumentAccess
            {
                DocumentId = documentId,
                GrantedToUserId = grantToUserId,
                AccessLevel = accessLevel,
                GrantedDate = DateTime.Now,
            };

            doc.SharedWith.Add(access);
            await _repo.UpdateDocumentAsync(doc);
            return true;
        }

        public async Task<Document> UploadDocumentAsync(string userId, string fileName, byte[] fileData)
        {
            var encryptionKey = _config["Vault:EncryptionKey"];
            var encryptedData = _encryptionService.EncryptData(fileData, encryptionKey);

            var document = new Document
            {
                FileName = fileName,
                FileSizeBytes = fileData.Length,
                UploadedDate = DateTime.UtcNow,
                UserId = userId,
                IsEncrypted = true,
                EncryptionAlgorithm = "AES-256",
                DocumentBlob = new DocumentBlob
                {
                    FileData = encryptedData,
                    FileHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(fileData)),
                    VersionNumber = 1
                }
            };
            return await _repo.AddDocumentAsync(document);
        }
    }
}

using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

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

            // 1. Get the raw text key from configuration
            var rawKey = _config["Vault:EncryptionKey"];

            // Fallback to JWT key if vault key is missing
            if (string.IsNullOrEmpty(rawKey))
            {
                rawKey = _config["JWT:SigningKey"];
            }

            if (string.IsNullOrEmpty(rawKey))
                throw new InvalidOperationException("Encryption key is not configured.");

            // 2. Hash the raw key ensuring exactly 32 bytes (256 bits) for AES
            var keyBytes = Encoding.UTF8.GetBytes(rawKey);
            var hashedBytes = SHA256.HashData(keyBytes);

            // 3. Convert to a perfectly valid Base64 string for the Encryption Service
            var base64Key = Convert.ToBase64String(hashedBytes);

            return _encryptionService.DecryptData(doc.DocumentBlob.FileData, base64Key);
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
            if (doc == null) return false;

            // Only owner may share
            if (doc.UserId != ownerUserId) return false;

            // Resolve target user: try Id, then UserName, then Email
            var targetUser = await _context.Users
                .FirstOrDefaultAsync( u =>
                u.Id == grantToUserId
                || u.UserName == grantToUserId
                || u.Email == grantToUserId);

            if (targetUser == null)
            {
                // Target user not found - do not attempt to create the DocumentAccess
                return false;
            }

            var access = new DocumentAccess
            {
                DocumentId = documentId,
                GrantedToUserId = targetUser.Id,
                AccessLevel = accessLevel,
                GrantedDate = DateTime.UtcNow,
            };

            doc.SharedWith.Add(access);
            await _repo.UpdateDocumentAsync(doc);
            return true;
        }

        public async Task<Document> UploadDocumentAsync(string userId, string fileName, byte[] fileData, int documentTypeId)
        {
            // 1. Get the raw text key from configuration
            var rawKey = _config["Vault:EncryptionKey"];

            // Fallback to JWT key if vault key is missing
            if (string.IsNullOrEmpty(rawKey))
            {
                rawKey = _config["JWT:SigningKey"];
            }

            // 2. Hash the raw key ensuring exactly 32 bytes (256 bits) for AES
            var keyBytes = Encoding.UTF8.GetBytes(rawKey);
            var hashedBytes = SHA256.HashData(keyBytes);

            // 3. Convert to a perfectly valid Base64 string for the Encryption Service
            var base64Key = Convert.ToBase64String(hashedBytes);

            // 4. Encrypt the document data
            var encryptedData = _encryptionService.EncryptData(fileData, base64Key);

            // Validate document type
            var documentType = await _context.DocumentTypes.FindAsync(documentTypeId);
            if (documentType == null)
            {
                // Handle the error gracefully, e.g., throw a custom exception or return an error result
                throw new ArgumentException($"Invalid DocumentTypeId. The document type '{documentTypeId}' does not exist.");
            }

            var document = new Document
            {
                FileName = fileName,
                FileSizeBytes = fileData.Length,
                UploadedDate = DateTime.UtcNow,
                UserId = userId,
                IsEncrypted = true,
                EncryptionAlgorithm = "AES-256",
                DocumentTypeId = documentTypeId,
                DocumentBlob = new DocumentBlob
                {
                    FileData = encryptedData,
                    FileHash = Convert.ToBase64String(SHA256.HashData(fileData)),
                    VersionNumber = 1
                }
            };
            return await _repo.AddDocumentAsync(document);
        }
    }
}

using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Security;
using FourierIT_API.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
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
        private readonly IFileScanService _fileScanService;
        private readonly IAuditLogService _auditLogService;
        private readonly DocumentValidityCalculator _documentValidityCalculator;

        private static readonly string[] AllowedExtensions = new[]
        {
            ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff", ".docx", ".xlsx", ".pptx", ".txt"
        };

        public DocumentService(
            IDocumentRepository repo,
            IEncryptionService encryptionService,
            IConfiguration config,
            AppDbContext context,
            IFileScanService fileScanService,
            IAuditLogService auditLogService,
            DocumentValidityCalculator? documentValidityCalculator = null)
        {
            _repo = repo;
            _encryptionService = encryptionService;
            _config = config;
            _context = context;
            _fileScanService = fileScanService;
            _auditLogService = auditLogService;
            _documentValidityCalculator = documentValidityCalculator ?? new DocumentValidityCalculator();
        }

        public async Task<byte[]> DownloadDocumentAsync(int documentId, string userId)
        {
            var doc = await _repo.GetDocumentByIdAsync(documentId);
            if (doc == null) throw new UnauthorizedAccessException("Document not found");

            //Check access: owner only
            if (doc.UserId != userId)
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
            return await _repo.GetUserDocumentAsync(userId);
        }

        public async Task<Document> UploadDocumentAsync(string userId, string fileName, byte[] fileData, int documentTypeId, DateTimeOffset? certificationDate = null)
        {
            ValidateFileExtension(fileName);

            using var fileStream = new MemoryStream(fileData);
            var scanResult = await _fileScanService.ScanFileAsync(fileStream);
            if (!scanResult.IsClean)
            {
                await TryCreateScanAuditLogAsync(userId, fileName, documentTypeId, scanResult);

                if (scanResult.Status == FileScanStatus.Infected)
                {
                    throw new InvalidOperationException(scanResult.Message);
                }

                throw new InvalidOperationException(scanResult.Message);
            }

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

            var uploadedDate = DateTime.UtcNow;
            var document = new Document
            {
                FileName = fileName,
                FileSizeBytes = fileData.Length,
                UploadedDate = uploadedDate,
                CurrentStatus = "Uploaded",
                UserId = userId,
                IsEncrypted = true,
                EncryptionAlgorithm = "AES-256",
                DocumentTypeId = documentTypeId,
                ExpiryDate = _documentValidityCalculator.Calculate(documentType, uploadedDate, certificationDate?.UtcDateTime).ExpiryDate,
                DocumentBlob = new DocumentBlob
                {
                    FileData = encryptedData,
                    FileHash = Convert.ToBase64String(SHA256.HashData(fileData)),
                    VersionNumber = 1
                }
            };
            return await _repo.AddDocumentAsync(document);
        }

        private static void ValidateFileExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is required.");
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unsupported file extension '{extension}'. Allowed extensions: {string.Join(", ", AllowedExtensions)}.");
            }
        }

        private async Task TryCreateScanAuditLogAsync(string userId, string fileName, int documentTypeId, FileScanResult scanResult)
        {
            try
            {
                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    UserId = userId,
                    ActionCode = scanResult.Status == FileScanStatus.Infected ? "DOCUMENT_UPLOAD_INFECTED" : "DOCUMENT_UPLOAD_SCAN_ERROR",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = $"File scan result for '{fileName}' (DocumentTypeId={documentTypeId}): {scanResult.Message}",
                    TableAffected = "Documents",
                    RecordID = null
                });
            }
            catch
            {
                // Audit logging must not stop file validation.
            }
        }
    }
}

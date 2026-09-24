namespace FourierIT_API.DTOs.Document
{
    public class DocumentResponseDto
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public bool IsCertified { get; set; }
        public bool IsEncrypted { get; set; }
        public string EncryptionAlgorithm { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime UploadedDate { get; set; }
        public DateTimeOffset ExpiryDate { get; set; }
        public bool NeverExpires { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
    }
}

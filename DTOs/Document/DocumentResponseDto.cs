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
        /// <summary>When the copy was certified (the latest certification), if it has one.</summary>
        public DateTimeOffset? CertificationDate { get; set; }
        public DateTimeOffset ExpiryDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public string UploadedByFirstName { get; set; } = string.Empty;
        public string UploadedByLastName { get; set; } = string.Empty;

        // Compliance review outcome: "Approved", "Rejected" or null when no decision has been made on the current file.
        public string? ReviewStatus { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}

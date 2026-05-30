namespace FourierIT_API.DTOs.Document
{
    public class UploadDocumentDto
    {
        public IFormFile File { get; set; } = null!;
        public int DocumentTypeId { get; set; }
        public bool IsCertified { get; set; } = false;
        public string? CommissionerName { get; set; }
        public DateTime? CertificationDate { get; set; }
    }
}

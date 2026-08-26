namespace FourierIT_API.DTOs.Document
{
    public class DocumentFlagDto
    {
        public int EnquiryFlagId { get; set; }
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string InstitutionName { get; set; } = string.Empty;
        public string FlagReason { get; set; } = string.Empty;
        public bool IsResolved { get; set; }
        public DateTimeOffset FlaggedAt { get; set; }
    }
}

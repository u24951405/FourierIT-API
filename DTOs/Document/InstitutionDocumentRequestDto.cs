namespace FourierIT_API.DTOs.Document
{
    public class InstitutionDocumentRequestDto
    {
        public string PurposeNote { get; set; } = string.Empty;
        public string TargetUserId { get; set; } = string.Empty;
        public List<RequestedDocumentTypeDto> RequestedDocuments { get; set; } = new();
    }
}

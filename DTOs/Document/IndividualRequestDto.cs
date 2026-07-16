namespace FourierIT_API.DTOs.Document
{
    /// <summary>
    /// DTO for creating a request to a specific individual document owner
    /// </summary>
    public class IndividualRequestDto
    {
        public string PurposeNote { get; set; } = string.Empty;
        public string TargetUserId { get; set; } = string.Empty;
        public List<RequestedDocumentTypeDto> RequestedDocuments { get; set; } = new();
    }
}

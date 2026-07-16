namespace FourierIT_API.DTOs.Document
{
    /// <summary>
    /// DTO for creating a request to a specific department
    /// </summary>
    public class DepartmentRequestDto
    {
        public string PurposeNote { get; set; } = string.Empty;
        public int TargetDepartmentId { get; set; }
        public List<RequestedDocumentTypeDto> RequestedDocuments { get; set; } = new();
    }
}

namespace FourierIT_API.DTOs.Document
{
    public class RequestedDocumentTypeDto
    {
        public int DocumentTypeId { get; set; }
        public int? FICARuleId { get; set; }
        public bool IsMandatory { get; set; } = true;
    }
}

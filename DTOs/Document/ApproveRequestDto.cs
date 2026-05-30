namespace FourierIT_API.DTOs.Document
{
    public class ApproveRequestDto
    {
        public string? UserResponseNote { get; set; }

        // Key: DocumentTypeID, Value: the user's DocumentId that satisfies that type
        public Dictionary<int, int> DocumentTypeToDocumentId { get; set; } = new();
    }
}

using FourierIT_API.Models;

namespace FourierIT_API.DTOs.Document
{
    public class ShareDocumentDto
    {
        public string GrantToUserId { get; set; } = string.Empty;
        public AccessLevel AccessLevel { get; set; }
        public DateTime? ExpiryDate {  get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}

namespace FourierIT_API.DTOs.Document
{
    public class DocumentAccessGrantDto
    {
        public int DocumentAccessId { get; set; }
        public string GrantedToUserId { get; set; } = string.Empty;
        public string GrantedToUserName { get; set; } = string.Empty;
        public int AccessLevel { get; set; }
        public DateTime GrantedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}

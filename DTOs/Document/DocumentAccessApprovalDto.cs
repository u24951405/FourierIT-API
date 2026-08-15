namespace FourierIT_API.DTOs.Document
{
    public class DocumentAccessApprovalDto
    {
        public int ApprovalId { get; set; }
        public int InstitutionId { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public string ApprovedByUserName { get; set; } = string.Empty;
        public DateTime ApprovedAt { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class InstitutionEnquiryRequest
    {
        [Key]
        public int EnquiryRequestId { get; set; }

        [ForeignKey("Institution")]
        public int InstitutionId { get; set; }
        public Institution Institution { get; set; } = null!;

        // Optional: Target a specific department first
        [ForeignKey("TargetDepartment")]
        public int? TargetDepartmentId { get; set; }
        public Department? TargetDepartment { get; set; }

        // Optional: Target a specific user (Document Owner)
        [ForeignKey("TargetUser")]
        public string? TargetUserId { get; set; }
        public User? TargetUser { get; set; }

        // Indicates whether this request is targeted at a department or individual
        [Required]
        [StringLength(20)]
        public string RequestType { get; set; } = "Individual"; // "Department" or "Individual"

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Denied, Routed_To_Owner, Department_Pending, etc.

        [Required]
        [StringLength(500)]
        public string PurposeNote { get; set; } = string.Empty;

        public DateTimeOffset? SubmissionDeadline { get; set; }

        [StringLength(150)]
        public string? ReferenceNumber { get; set; }

        public DateTimeOffset RequestDate { get; set; } = DateTimeOffset.UtcNow;

        public AccessToken AccessToken { get; set; } = null!;

        public DateTime? RespondedAt { get; set; }

        [StringLength(500)]
        public string? UserResponseNote { get; set; }

        // Track who approved this request (Department Admin or Document Owner)
        [ForeignKey("ApprovedByUser")]
        public string? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();
        public ICollection<InstitutionRequestedDocumentType> RequestedDocumentTypes { get; set; } = new List<InstitutionRequestedDocumentType>();
    }
}

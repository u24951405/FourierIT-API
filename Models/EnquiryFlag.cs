using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class EnquiryFlag
    {
        [Key]
        public int EnquiryFlagId { get; set; }

        [ForeignKey("Document")]
        public int DocumentId { get; set; }

        [ForeignKey("EnquiryComment")]
        public int EnquiryId { get; set; }
        public ICollection<EnquiryComment> EnquiryComments { get; set; } = new List<EnquiryComment>();

        [Required]
        [StringLength(500)]
        public string FlagReason { get; set; } = null!;

        public bool IsResolved { get; set; } = false;

        public DateTimeOffset? ResolvedAt { get; set; }

        /// <summary>What the owner did about the flag; passed on to the institution.</summary>
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? ResolutionNote { get; set; }

        public DateTimeOffset FlaggedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property for AccessList
        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();
    }
}

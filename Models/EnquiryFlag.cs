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
        public int EnquiryCommentId { get; set; }
        public ICollection<EnquiryComment> EnquiryComments { get; set; } = new List<EnquiryComment>();

        [Required]
        public string FlagReason { get; set; } = null!;

        public bool IsResolved { get; set; } = false;
        // Navigation property for AccessList
        public AccessList AccessList { get; set; } = null!;
    }
}

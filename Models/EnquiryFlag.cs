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

        // Navigation property for AccessList
        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Security.Principal;

namespace FourierIT_API.Models
{
    public class DocumentAccess
    {
        [Key]
        public int DocumentAccessId { get; set; }

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        [ForeignKey("GrantedToUser")]
        public string GrantedToUserId { get; set; } = string.Empty;
        public User GrantedToUser { get; set; } = null!;

        [Required]
        public AccessLevel AccessLevel { get; set; }

        [Required]
        public DateTime GrantedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiryDate { get; set; }

        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}

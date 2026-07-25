using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        //[ForeignKey(nameof(User))]
        [Required]
        [StringLength(450)]
        public String UserId { get; set; } = String.Empty;

        [Required]
        [StringLength(50)]
        public string ActionCode { get; set; } = string.Empty;

        public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;

        
        [StringLength(1000)]
        public string? Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TableAffected { get; set; } = string.Empty;

        public int? RecordID { get; set; }

        [StringLength(64)]
        public string? PreviousBlockHash { get; set; }

        [StringLength(64)]
        public string? BlockHash { get; set; }

        // Optional: Uncomment when ready to link to AspNetUsers table
        // [ForeignKey(nameof(UserId))]
        // public virtual User? User { get; set; }
    }
}

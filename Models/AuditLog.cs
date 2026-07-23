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
        public String UserId { get; set; } = String.Empty;

        [Required]
        [StringLength(20)]
        public string ActionCode { get; set; } = string.Empty;

        public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;

        
        [StringLength(300)]
        public string? Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TableAffected { get; set; } = string.Empty;

        public int? RecordID { get; set; }

        [StringLength(64)]
        public string? PreviousBlockHash { get; set; }

        [StringLength(64)]
        public string? BlockHash { get; set; }

        //navigation property to aspnetusers
        //public virtual User? User { get; set; }
    }
}

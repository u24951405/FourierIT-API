using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        /// <summary>
        /// Set when the actor is an external institution (e.g. the institution portal)
        /// rather than a logged-in system user. Mutually exclusive with UserId in practice.
        /// </summary>
        public int? InstitutionId { get; set; }

        [ForeignKey(nameof(InstitutionId))]
        public virtual Institution? Institution { get; set; }

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

    }
}

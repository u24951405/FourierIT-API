using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [StringLength(20)]
        public int ActionCode { get; set; }

        public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        [StringLength(300)]
        public string? Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TableAffected { get; set; } = string.Empty;
    }
}

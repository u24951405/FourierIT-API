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
        public int ActionCode { get; set; }

        public string? Description { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Models
{
    public class PEPList
    {
        [Key]
        public int UniqueId { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Position { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string SourceLinks { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Aliases { get; set; } = string.Empty;
    }
}

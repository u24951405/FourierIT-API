using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Models
{
    public class PEPList
    {
        [Key]
        public int UniqueId { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Position { get; set; } = string.Empty;

        [Required]
        public string SourceLinks { get; set; } = string.Empty;

        [Required]
        public string Aliases { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class FICARuleHistory
    {
        [Key]
        public int RuleHistoryId { get; set; }

        [Required]
        public DateTimeOffset DateChanged { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey("FICARule")]
        public int RuleId { get; set; }

        public FICARule FICARule { get; set; } = null!;
    }
}

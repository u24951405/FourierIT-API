using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class FAQ
    {
        [Key]
        public int FAQId { get; set; }

        [Required]
        [StringLength(500)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Answer { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;
    }
}

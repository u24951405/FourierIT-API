using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class SecurityQuestion
    {
        [Key]
        public int SecurityQuestionId { get; set; }

        [Required]
        [StringLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        public ICollection<UserSecurityQuestion> UserSecurityQuestions { get; set; } = new List<UserSecurityQuestion>();
    }
}

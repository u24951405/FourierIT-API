using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class SecurityQuestion
    {
        [Key]
        public int SecurityQuestionId { get; set; }

        [Required]
        public string Question { get; set; } = string.Empty;

        public ICollection<UserSecurityQuestion> UserSecurityQuestions { get; set; } = new List<UserSecurityQuestion>();
    }
}

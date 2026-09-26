using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.User
{
    public class RequestEmailChangeDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string NewEmail { get; set; } = string.Empty;
    }

    public class VerifyEmailChangeDto
    {
        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code from the email.")]
        public string Otp { get; set; } = string.Empty;
    }
}

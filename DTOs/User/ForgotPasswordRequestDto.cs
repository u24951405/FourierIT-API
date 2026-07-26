using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.User
{
    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;
    }
}

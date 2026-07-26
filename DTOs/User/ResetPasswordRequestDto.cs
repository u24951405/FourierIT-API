using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.User
{
    public class ResetPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }
}

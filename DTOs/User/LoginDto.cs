using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.User
{
    public class LoginDto
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
    }
}

using FourierIT_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace FourierIT_API.DTOs.User  
{
    public class UserDto
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;
        [Required]
        public string LastName { get; set; } = string.Empty;
        [Required]
        public DateOnly DateOfBirth { get; set; } = new DateOnly();
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        public string JobTitle { get; set; } = string.Empty;
        [Required] 
        public string Username { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;
        [Required]
        public string? Password { get; set; } = null;

        // Role selected by the user during registration
        // Must match one of the seeded roles
        [Required]
        public string Role { get; set; } = string.Empty;
    }
}

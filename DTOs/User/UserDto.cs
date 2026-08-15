using FourierIT_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Collections.Generic;

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

        public int? EntityTypeId { get; set; }

        public string? EntityIdentificationNumber { get; set; }

        // Up to two roles can be selected during registration.
        // Role is kept for backward compatibility with older clients.
        public string Role { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class VerifyRegistrationOtpRequestDto
    {
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        public string Otp { get; set; } = string.Empty;
    }
}

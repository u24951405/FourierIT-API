using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FourierIT_API.DTOs.User
{
    public class NewUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateOnly DateOfBirth { get; set; } = new DateOnly();
        public string PhoneNumber { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
    }
}

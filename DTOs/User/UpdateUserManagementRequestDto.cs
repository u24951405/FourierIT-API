using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.User
{
    public class UpdateUserManagementRequestDto
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
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string AccountStatus { get; set; } = "Active";
    }
}

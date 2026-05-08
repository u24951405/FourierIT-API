namespace FourierIT_API.DTOs.Profile
{
    public class UpdateProfileRequestDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; } = new DateOnly();
        public string PhoneNumber { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
    }
}

namespace FourierIT_API.DTOs.User
{
    public class CurrentAccountDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public List<string> Roles { get; set; } = new();
        public int? ProfileId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePhoneNumber { get; set; }
        public string? JobTitle { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
    }
}

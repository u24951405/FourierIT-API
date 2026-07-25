namespace FourierIT_API.DTOs.User
{
    public class CreateDepartmentAdminRequestDto
    {
        public int DepartmentId { get; set; }
        public string? UserId { get; set; } // Optional: if provided, assign existing user; if not, create new admin user
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? EmailAddress { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
        public string? JobTitle { get; set; }
    }

    public class DepartmentAdminDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
    }
}

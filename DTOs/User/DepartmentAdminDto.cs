namespace FourierIT_API.DTOs.User
{
    public class CreateDepartmentAdminRequestDto
    {
        public int DepartmentId { get; set; }
        public string? UserId { get; set; }
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
        public DateOnly? DateOfBirth { get; set; }
    }
}

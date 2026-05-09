namespace FourierIT_API.DTOs.Role
{
    public class UpdateRoleRequestDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? NewRoleId { get; internal set; }
    }
}

namespace FourierIT_API.DTOs.Role
{
    public class RolePermissionDto
    {
        public int PermissionId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }

    public class RoleDetailDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<RolePermissionDto> Permissions { get; set; } = new();
    }

    public class AddPermissionToRoleDto
    {
        public string RoleId { get; set; } = string.Empty;
        public int PermissionId { get; set; }
    }

    public class RemovePermissionFromRoleDto
    {
        public string RoleId { get; set; } = string.Empty;
        public int PermissionId { get; set; }
    }
}

using System.Globalization;

namespace FourierIT_API.DTOs.User
{
    public class ReplaceRoleDto
    {
        public string OldRole { get; set; } = string.Empty;
        public string NewRole { get; set; } = string.Empty;
    }
}

using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Role : IdentityRole
    {
        // Keep the explicit join entity collection (authoritative)
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}

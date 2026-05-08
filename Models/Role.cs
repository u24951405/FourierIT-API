using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Role
    {
        public int RoleId { get; set; }

        [Required]
        [StringLength(100)]
        public string RoleName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Keep the explicit join entity collection (authoritative)
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}

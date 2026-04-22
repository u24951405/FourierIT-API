using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required]
        [MaxLength(100)]
        public int PermissionKey { get; set; } = 0;

        public ICollection<RolePermission> RolePermissions { get; set;} = new List<RolePermission>();
    }
}

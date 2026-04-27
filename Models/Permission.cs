using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required]
        [StringLength(100)]
        public string PermissionKey { get; set; } = string.Empty;

        public ICollection<RolePermission> RolePermissions { get; set;} = new List<RolePermission>();
    }
}

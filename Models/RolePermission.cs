using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class RolePermission
    {
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        
        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

    }
}

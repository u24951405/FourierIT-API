using System.Globalization;

namespace FourierIT_API.Models
{
    public class UserRole
    {
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        //Metadata 
        public bool IsActiveContext { get; set; } = true;
    }
}
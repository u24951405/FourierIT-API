using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNetCore.Identity;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class UserRole : IdentityUserRole
    {
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        public string RoleId { get; set; } = string.Empty;
        public Role Role { get; set; } = null!;
        //Metadata 
        public bool IsActiveContext { get; set; } = true;
    }
}
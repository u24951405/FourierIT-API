
using Microsoft.AspNetCore.Identity;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class UserRole : IdentityUserRole<string>
    {
        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
        //Metadata 
        public bool IsActiveContext { get; set; } = true;
    }
}
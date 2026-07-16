using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace FourierIT_API.Models
{
    public class User : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public bool MfaEnabled { get; set; } = false;

        [Required]
        [MaxLength(500)]
        public int FailedLoginAttempts { get; set; } = 0;

        public bool IsPEPStatus { get; set; } = false;

        [MaxLength(20)]
        public string AccountStatus { get; set; } = "Active"; // Deactivated, Suspended, etc.

        public bool HasAccessToken { get; set; } = false;

        // Keep the explicit join entity collection (authoritative for the many-to-many)
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        // One-to-one Profile navigation (shared PK configured in DbContext)
        public Profile Profile { get; set; } = null!;

        // One to-many relationship with Document (a user can have multiple documents)
        public ICollection<Document> Documents { get; set; } = new List<Document>();

        // One-to-many relationship with UserSecurityQuestion (a user can have multiple security questions)
        public ICollection<UserSecurityQuestion> UserSecurityQuestions { get; set; } = new List<UserSecurityQuestion>();

        // One-to-many relationship with UserNotification (a user can have multiple notifications)
        public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();

        // One-to-many relationship with InstitutionMembers
        public ICollection<InstitutionMembers> InstitutionMembers { get; set; } = new List<InstitutionMembers>();

        // One-to-many relationship with ClientEnlistment
        public ICollection<ClientEnlistment> ClientEnlistments { get; set; } = new List<ClientEnlistment>();

        // One-to-one relationship with AccessToken (a user has one access token)
        public AccessToken AccessToken { get; set; } = null!;

        public EnquiryComment EnquiryComment { get; set; } = null!;

        public int? EntityTypeId { get; set; }
        public EntityType? EntityType { get; set; }

        // Many-to-one relationship with Department (a user belongs to a department)
        public int? DepartmentId { get; set; }

        [ForeignKey(nameof(DepartmentId))]
        public Department? Department { get; set; }
    }
}

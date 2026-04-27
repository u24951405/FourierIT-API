using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class Profile
    {
        [Key]
        public int ProfileId { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public DateOnly DateOfBirth { get; set; } = new DateOnly();

        [Required]
        [StringLength(15)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string JobTitle { get; set; } = string.Empty;

        // Navigation properties for one-to-one relationships with user and address entities, so that we can easily access the user and address information from the profile entity.    
        [ForeignKey("User")]
        public int UserId { get; set; }

        public User User { get; set; } = null!;

        [ForeignKey("Address")]
        public int AddressId { get; set; }

        public Address Address { get; set; } = null!;
    }
}

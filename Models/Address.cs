using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class Address
    {
        [Key]
        public int AddressId { get; set; }
        public string AddressLine { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;

        // Foreign key for Province
        [ForeignKey("Province")]
        public int ProvinceId { get; set; }
        public Province Province { get; set; } = null!;

        // Navigation property for the one-to-one relationship with Profile, so that we can easily access the profile information from the address entity.
        [ForeignKey("Profile")]
        public int ProfileId { get; set; }       
        public Profile Profile { get; set; } = null!;
    }
}

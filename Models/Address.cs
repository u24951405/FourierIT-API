using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class Address
    {
        [Key]
        public int AddressId { get; set; }

        [Required]
        [StringLength(255)]
        public string AddressLine { get; set; } = string.Empty;
        public int PostalCode { get; set; }

        // Foreign key for Province
        [ForeignKey("Province")]
        public int SuburbID { get; set; }
        public Suburb Suburb { get; set; } = null!;

        // Navigation property for the one-to-one relationship with Profile, so that we can easily access the profile information from the address entity.     
        public Profile Profile { get; set; } = null!;
    }
}

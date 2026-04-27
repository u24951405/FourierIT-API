using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class Suburb
    {
        [Key]
        public int SuburbId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SuburbName { get; set; } = string.Empty;

        [Required]
        public bool IsAtRisk { get; set; } = false;

        [ForeignKey("City")]
        public int CityId { get; set; }

        public City City { get; set; } = null!;

        public ICollection<Address> Addresses { get; set; } = new List<Address>();
    }
}

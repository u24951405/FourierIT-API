using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class City
    {
        [Key]
        public int CityId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CityName { get; set; } = string.Empty;

        // Foreign key for Province
        [ForeignKey("Province")]
        public int ProvinceId { get; set; }
        public Province Province { get; set; } = null!;

        public ICollection<Suburb> Suburbs { get; set; } = new List<Suburb>();

    }
}

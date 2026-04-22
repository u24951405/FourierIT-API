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
        public string SuburbName { get; set; } = string.Empty;

        [Required]
        public bool IsAtRisk { get; set; } = false;

        [ForeignKey("City")]
        public int CityId { get; set; }

        public City City { get; set; } = null!;
    }
}

using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Province
    {
        [Key]
        public int ProvinceId { get; set; }
        [Required]
        public string ProvinceName { get; set; } = string.Empty;
        // public string Code { get; set; } = string.Empty;

        // Navigation properties for the one-to-many relationship with City and address entities
        public ICollection<City> Cities { get; set; } = new List<City>();

        public ICollection<Address> Addresses { get; set; } = new List<Address>();

    }
}

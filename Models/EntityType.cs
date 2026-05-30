using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class EntityType
    {
        [Key]
        public int EntityTypeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public ICollection<RequiredDocument> RequiredDocuments { get; set; } = new List<RequiredDocument>();
    }
}

using Microsoft.EntityFrameworkCore.Query;
using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class InstitutionType
    {
        [Key]
        public int InstitutionTypeId { get; set; }

        [Required]
        [StringLength(100)]
        public string InstitutionTypeName { get; set; } = string.Empty;

        public ICollection<Institution> Institutions { get; set; } = new List<Institution>();
    }
}

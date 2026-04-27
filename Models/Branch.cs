using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class Branch
    {
        [Key]
        public int BranchId { get; set; }

        [ForeignKey("Institution")]
        public int InstitutionId { get; set; }
        public Institution Institution { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string BranchName { get; set; } = string.Empty;

        [StringLength(150)]
        public string City { get; set; } = string.Empty;

        public ICollection<Department> Departments { get; set; } = new List<Department>();
    }
}

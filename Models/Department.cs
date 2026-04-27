using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class Department
    {
        [Key]
        public int DepartmentId { get; set; }

        [ForeignKey("Branch")]
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string DepartmentName { get; set; } = string.Empty;
    }
}

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
        //this is the date of creation of the department 
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        [StringLength(150)]
        public string DepartmentName { get; set; } = string.Empty;
    }

}

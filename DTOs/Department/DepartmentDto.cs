using FourierIT_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.DTOs.Department
{
    public class DepartmentDto
    {
 
        public int DepartmentId { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public string DepartmentName { get; set; } = string.Empty;
    }
}

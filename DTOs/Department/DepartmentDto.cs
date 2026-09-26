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

        // A department's only user is its Department Admin (null when none is assigned).
        public string? AdminUserId { get; set; }
        public string? AdminName { get; set; }
        public string? AdminEmail { get; set; }

        // Documents uploaded by the department (i.e. by its Department Admin), excluding deleted ones.
        public int DocumentCount { get; set; }
    }
}

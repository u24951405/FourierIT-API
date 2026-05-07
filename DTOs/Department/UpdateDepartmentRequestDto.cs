using FourierIT_API.Models;

namespace FourierIT_API.DTOs.Department
{
    public class UpdateDepartmentRequestDto
    {

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;


        public string DepartmentName { get; set; } = string.Empty;
    }
}

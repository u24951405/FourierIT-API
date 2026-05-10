namespace FourierIT_API.DTOs.Department
{
    public class UpdateDepartmentRequestDto
    {
        public int BranchId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }
}

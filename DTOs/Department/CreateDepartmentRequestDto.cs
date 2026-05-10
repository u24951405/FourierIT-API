namespace FourierIT_API.DTOs.Department
{
    public class CreateDepartmentRequestDto
    {
        public int BranchId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }
}

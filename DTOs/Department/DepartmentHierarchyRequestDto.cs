namespace FourierIT_API.DTOs.Department
{
    public class DepartmentHierarchyRequestDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int? ParentId { get; set; }
    }
}

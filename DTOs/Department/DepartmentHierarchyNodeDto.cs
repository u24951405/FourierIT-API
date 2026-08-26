namespace FourierIT_API.DTOs.Department
{
    public class DepartmentHierarchyNodeDto
    {
        public int DepartmentId { get; set; }
        public int? ParentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public List<DepartmentHierarchyNodeDto> Children { get; set; } = new();
    }
}

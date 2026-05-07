using FourierIT_API.DTOs.Department;
using FourierIT_API.Models;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace FourierIT_API.Mappers
{
    public  static class DepartmentMappers
    {

        public static DepartmentDto ToDepartmentDto(this Department departmentModel)
        {
            return new DepartmentDto
            {
                DepartmentId = departmentModel.DepartmentId,
                DepartmentName = departmentModel.DepartmentName,
                BranchId = departmentModel.BranchId,
                Branch = departmentModel.Branch,
                CreatedAt = departmentModel.CreatedAt


            };
        }

        public static  Department ToDepartmentFromCreatDTO( this CreateDepartmentRequestDto DepartmentDto)
        {
            return new Department
            {
                DepartmentName=DepartmentDto.DepartmentName,
                BranchId=DepartmentDto.BranchId,
                Branch=DepartmentDto.Branch,
                CreatedAt=DepartmentDto.CreatedAt,


            };
        }
    }
}

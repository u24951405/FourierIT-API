using FourierIT_API.DTOs.Department;
using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IDepartmentRepository
    {

        //The point of the interface is that we wall all out database call out of our controllerrs, the controller is for manipulating the urls
        Task<List<Department>> GetAllAsync();

        Task<Department?> GetByIdAsync(int DepartmentId);//? means that it can be null, if we try to get a department by id and it doesn't exist, we will return null
        Task<Department> CreateAsync(Department departmentModel);

        Task<Department> UpdateAsync(int DepartmentId,UpdateDepartmentRequestDto DepartmentDto);

        Task<Department?> DeleteAsync(int DepartmentId);
    }
}

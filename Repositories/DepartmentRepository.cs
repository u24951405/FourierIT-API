using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Repositories
{
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly AppDbContext _context;
        public DepartmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public  async Task<Department> CreateAsync(Department departmentModel)
        {
            await _context.Departments.AddAsync(departmentModel);
            await _context.SaveChangesAsync();
            return departmentModel;
        }

        public async Task<Department> DeleteAsync(int DepartmentId)
        {
           var departmentModel = await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == DepartmentId);
            if (departmentModel == null)
            {
                return null;
            }
            _context.Departments.Remove(departmentModel);
            await _context.SaveChangesAsync();
            return departmentModel;
        }

        public async Task<List<Department>> GetAllAsync()
        {
            return await _context.Departments.ToListAsync();
        }

        public async Task<Department?> GetByIdAsync(int DepartmentId)
        {
           return await _context.Departments.FindAsync(DepartmentId);  
        }

        public async Task<Department> UpdateAsync(int DepartmentId, UpdateDepartmentRequestDto DepartmentDto)
        {
           var existingDepartment = await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == DepartmentId);
            if (existingDepartment == null)
            {
                return null;
            }
            existingDepartment.DepartmentName = DepartmentDto.DepartmentName;
            existingDepartment.BranchId = DepartmentDto.BranchId;
            existingDepartment.CreatedAt = DepartmentDto.CreatedAt;
            existingDepartment.Branch = DepartmentDto.Branch;   
            
            await _context.SaveChangesAsync();
            return existingDepartment;
        }
    }
    
}

using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.Interfaces;
using FourierIT_API.Mappers;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/Department")]
    [ApiController]
    [Authorize(Roles = "Department Admin")]
    public class DepartmentController : ControllerBase
    {
        private readonly AppDbContext _context; // create a private variable to hold our database context and prevents it from being mutable
        private readonly IDepartmentRepository _departmentRepo;
        public DepartmentController(AppDbContext context, IDepartmentRepository departmentRepo)// bring in our database context to the controller
        {
            _departmentRepo = departmentRepo;
            _context = context;
        }

        [HttpGet]

        public async Task< IActionResult> GetAll()
        {
            var departments = await _departmentRepo.GetAllAsync();//  deffered excecution to get all the departments from the database and convert it to a list

            var departmentDto = departments.Select(d => d.ToDepartmentDto());//  deffered excecution to get all the departments from the database and convert it to a list
            return Ok(departments);
        }


        [HttpGet("{DepartmentId}")]
        public async  Task<IActionResult> GetById([FromRoute] int DepartmentId)//  get a specific department by its id, the id is passed as a parameter in the route and is marked with [FromRoute] to indicate that it should be bound from the route data
        {
            var department = await _departmentRepo.GetByIdAsync(DepartmentId);//  deffered excecution to find a department by its id using the Find method of the database context, which will return null if no department is found with the specified id
            if (department == null)
            {
                return NotFound();
            }
            return Ok(department.ToDepartmentDto());
        }

        [HttpPost]
        public async Task<IActionResult> create([FromBody] CreateDepartmentRequestDto DepartmentDto)
        {
            var departmentModel = DepartmentDto.ToDepartmentFromCreatDTO();
            await _departmentRepo.CreateAsync(departmentModel);
            return CreatedAtAction(nameof(GetById),new {id=departmentModel.DepartmentId} ,departmentModel.ToDepartmentDto());
        }


        [HttpPut]
        [Route("{DepartmentId}")]
        public async Task<IActionResult> Update([FromRoute] int DepartmentId, [FromBody] UpdateDepartmentRequestDto UpdateDto)
        {
            var departmentModel = await _departmentRepo.UpdateAsync(DepartmentId, UpdateDto);
            if (departmentModel == null)
            {
                return NotFound();
            }
          
            return Ok(departmentModel.ToDepartmentDto());

        }

        [HttpDelete]
        [Route("{DepartmentId}")]
        public async Task<IActionResult> Delete([FromRoute] int DepartmentId)
        {
            var departmentModel =await _departmentRepo.DeleteAsync(DepartmentId);
            if (departmentModel == null)
            {
                return NotFound();
            }
           
            return NoContent();
        }
    }
}

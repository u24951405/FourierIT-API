using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.Mappers;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/Department")]
    [ApiController]
    public class DepartmentController : ControllerBase
    {
        private readonly AppDbContext _context; // create a private variable to hold our database context and prevents it from being mutable
        private readonly UserManager<User> _userManager;
        public DepartmentController(AppDbContext context, UserManager<User> userManager)// bring in our database context to the controller
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (await _userManager.IsInRoleAsync(user, "Department Admin") && !await _userManager.IsInRoleAsync(user, "Super Admin"))
            {
                if (!user.DepartmentId.HasValue)
                {
                    return Ok(Array.Empty<DepartmentDto>());
                }

                var department = await _context.Departments
                    .AsNoTracking()
                    .Where(d => d.DepartmentId == user.DepartmentId.Value)
                    .ToListAsync();

                return Ok(department.Select(d => d.ToDepartmentDto()));
            }

            var departments = await _context.Departments.AsNoTracking().ToListAsync();
            return Ok(departments.Select(d => d.ToDepartmentDto()));
        }


        [Authorize]
        [HttpGet("{DepartmentId}")]
        public async Task<IActionResult> GetById([FromRoute] int DepartmentId)//  get a specific department by its id, the id is passed as a parameter in the route and is marked with [FromRoute] to indicate that it should be bound from the route data
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (await _userManager.IsInRoleAsync(user, "Department Admin") && !await _userManager.IsInRoleAsync(user, "Super Admin"))
            {
                if (!user.DepartmentId.HasValue || user.DepartmentId.Value != DepartmentId)
                {
                    return Forbid();
                }
            }

            var department = await _context.Departments.FindAsync(DepartmentId);//  deffered excecution to find a department by its id using the Find method of the database context, which will return null if no department is found with the specified id
            if (department == null)
            {
                return NotFound();
            }
            return Ok(department.ToDepartmentDto());
        }

        [Authorize(Roles = "Super Admin")]
        [HttpPost]
        public async Task<IActionResult> create([FromBody] CreateDepartmentRequestDto DepartmentDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (string.IsNullOrWhiteSpace(DepartmentDto.DepartmentName))
                return BadRequest(new { error = "Department name is required." });
            if (!await _context.Branches.AnyAsync(b => b.BranchId == DepartmentDto.BranchId))
                return BadRequest(new { error = "Invalid branch. Select an existing branch." });

            var departmentModel = DepartmentDto.ToDepartmentFromCreatDTO();
            await _context.Departments.AddAsync(departmentModel);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { DepartmentId = departmentModel.DepartmentId }, departmentModel.ToDepartmentDto());
        }


        [Authorize(Roles = "Department Admin")]
        [HttpPut]
        [Route("{DepartmentId}")]
        public async Task<IActionResult> Update([FromRoute] int DepartmentId, [FromBody] UpdateDepartmentRequestDto UpdateDto)
        {
            // Ensure Department Admins can only update their own department
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (await _userManager.IsInRoleAsync(user, "Department Admin") && !await _userManager.IsInRoleAsync(user, "Super Admin"))
            {
                if (!user.DepartmentId.HasValue || user.DepartmentId.Value != DepartmentId)
                {
                    return Forbid();
                }
            }

            var departmentModel = await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == DepartmentId);
            if (departmentModel == null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(UpdateDto.DepartmentName))
                return BadRequest(new { error = "Department name is required." });
            if (!await _context.Branches.AnyAsync(b => b.BranchId == UpdateDto.BranchId))
                return BadRequest(new { error = "Invalid branch." });

            departmentModel.DepartmentName = UpdateDto.DepartmentName.Trim();
            departmentModel.BranchId = UpdateDto.BranchId;

            await _context.SaveChangesAsync();
            return Ok(departmentModel.ToDepartmentDto());

        }

        [Authorize(Roles = "Super Admin")]
        [HttpDelete]
        [Route("{DepartmentId}")]
        public async Task<IActionResult> Delete([FromRoute] int DepartmentId)
        {
            
            var departmentModel =await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == DepartmentId);
            if (departmentModel == null)
            {
                return NotFound();
            }

            //this removes the department which was deleted
            _context.Departments.Remove(departmentModel);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Get all required document types for a specific department
        /// </summary>
        [Authorize]
        [HttpGet("{DepartmentId}/required-documents")]
        public async Task<IActionResult> GetRequiredDocuments([FromRoute] int DepartmentId)
        {
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == DepartmentId);
            if (department == null)
                return NotFound(new { error = "Department not found." });

            var requiredDocs = await _context.DepartmentDocumentTypes
                .Where(ddt => ddt.DepartmentId == DepartmentId)
                .Include(ddt => ddt.DocumentType)
                .AsNoTracking()
                .ToListAsync();

            var result = requiredDocs.Select(ddt => new
            {
                departmentDocumentTypeId = ddt.DepartmentDocumentTypeId,
                documentTypeId = ddt.DocumentTypeId,
                documentTypeName = ddt.DocumentType?.TypeName ?? string.Empty,
                isMandatory = ddt.IsMandatory,
                createdAt = ddt.CreatedAt
            }).ToList();

            return Ok(result);
        }
    }
}

using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.Mappers;
using FourierIT_API.Models;
using FourierIT_API.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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

                return Ok(await WithAdminDetailsAsync(department.Select(d => d.ToDepartmentDto()).ToList()));
            }

            var departments = await _context.Departments.AsNoTracking().ToListAsync();
            return Ok(await WithAdminDetailsAsync(departments.Select(d => d.ToDepartmentDto()).ToList()));
        }

        /// <summary>Adds each department's Department Admin (its only user) and document count.</summary>
        private async Task<List<DepartmentDto>> WithAdminDetailsAsync(List<DepartmentDto> departments)
        {
            var departmentIds = departments.Select(d => d.DepartmentId).ToList();
            if (departmentIds.Count == 0) return departments;

            var admins = await _context.Users
                .AsNoTracking()
                .Where(u => u.DepartmentId.HasValue && departmentIds.Contains(u.DepartmentId.Value))
                .Select(u => new
                {
                    u.Id,
                    DepartmentId = u.DepartmentId!.Value,
                    u.Email,
                    u.UserName,
                    FirstName = u.Profile != null ? u.Profile.FirstName : null,
                    LastName = u.Profile != null ? u.Profile.LastName : null
                })
                .ToListAsync();

            var adminIds = admins.Select(a => a.Id).ToList();
            var documentCounts = await _context.Documents
                .AsNoTracking()
                .Where(d => adminIds.Contains(d.UserId) && d.CurrentStatus != "Deleted")
                .GroupBy(d => d.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            foreach (var department in departments)
            {
                var admin = admins.FirstOrDefault(a => a.DepartmentId == department.DepartmentId);
                if (admin == null) continue;

                var fullName = $"{admin.FirstName} {admin.LastName}".Trim();
                department.AdminUserId = admin.Id;
                department.AdminName = string.IsNullOrWhiteSpace(fullName) ? admin.UserName : fullName;
                department.AdminEmail = admin.Email;
                department.DocumentCount = documentCounts.GetValueOrDefault(admin.Id);
            }

            return departments;
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
            return Ok((await WithAdminDetailsAsync(new List<DepartmentDto> { department.ToDepartmentDto() }))[0]);
        }

        // Departments are registered by the Super Admin only.
        [Authorize(Policy = "SuperAdminOnly")]
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

        [Authorize(Policy = "Users.Manage")]
        [HttpGet("hierarchy")]
        public async Task<IActionResult> GetHierarchy()
        {
            var allDepartments = await _context.Departments
                .AsNoTracking()
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();

            var roots = allDepartments
                .Where(d => d.ParentId == null)
                .Select(d => BuildHierarchyNode(d, allDepartments))
                .ToList();

            return Ok(roots);
        }

        // Departments are registered by the Super Admin only.
        [Authorize(Policy = "SuperAdminOnly")]
        [HttpPost("hierarchy")]
        public async Task<IActionResult> CreateHierarchyNode([FromBody] DepartmentHierarchyRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.DepartmentName))
                return BadRequest(new { error = "Department name is required." });

            if (!await _context.Branches.AnyAsync(b => b.BranchId == request.BranchId))
                return BadRequest(new { error = "Invalid branch. Select an existing branch." });

            if (request.ParentId.HasValue && request.ParentId.Value <= 0)
                return BadRequest(new { error = "Parent department is invalid." });

            if (request.ParentId.HasValue && !await _context.Departments.AnyAsync(d => d.DepartmentId == request.ParentId.Value))
                return BadRequest(new { error = "Parent department was not found." });

            var department = new Department
            {
                DepartmentName = request.DepartmentName.Trim(),
                BranchId = request.BranchId,
                ParentId = request.ParentId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _context.Departments.AddAsync(department);
            await _context.SaveChangesAsync();

            var allDepartments = await _context.Departments.AsNoTracking().ToListAsync();
            return Ok(BuildHierarchyNode(department, allDepartments));
        }

        // Only the Super Admin manages departments; Department Admins cannot edit or delete them.
        [Authorize(Policy = "SuperAdminOnly")]
        [HttpPut("hierarchy/{departmentId:int}")]
        public async Task<IActionResult> UpdateHierarchyNode([FromRoute] int departmentId, [FromBody] DepartmentHierarchyRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var department = await _context.Departments
                .Include(d => d.Children)
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);

            if (department == null)
                return NotFound(new { error = "Department not found." });

            if (string.IsNullOrWhiteSpace(request.DepartmentName))
                return BadRequest(new { error = "Department name is required." });

            if (!await _context.Branches.AnyAsync(b => b.BranchId == request.BranchId))
                return BadRequest(new { error = "Invalid branch. Select an existing branch." });

            if (request.ParentId == departmentId)
                return BadRequest(new { error = "A department cannot be its own parent." });

            if (request.ParentId.HasValue && request.ParentId.Value != department.ParentId)
            {
                var allDepartments = await _context.Departments.AsNoTracking().ToListAsync();
                var targetParent = allDepartments.FirstOrDefault(d => d.DepartmentId == request.ParentId.Value);
                if (targetParent == null)
                    return BadRequest(new { error = "Parent department was not found." });

                if (IsDescendantOf(departmentId, request.ParentId.Value, allDepartments))
                    return BadRequest(new { error = "A department cannot be moved beneath one of its own children." });
            }

            department.DepartmentName = request.DepartmentName.Trim();
            department.BranchId = request.BranchId;
            department.ParentId = request.ParentId;

            await _context.SaveChangesAsync();

            var allDepartmentsAfter = await _context.Departments.AsNoTracking().ToListAsync();
            return Ok(BuildHierarchyNode(department, allDepartmentsAfter));
        }

        // Only the Super Admin manages departments; Department Admins cannot edit or delete them.
        [Authorize(Policy = "SuperAdminOnly")]
        [HttpDelete("hierarchy/{departmentId:int}")]
        public async Task<IActionResult> DeleteHierarchyNode([FromRoute] int departmentId)
        {
            var department = await _context.Departments
                .Include(d => d.Children)
                .Include(d => d.DepartmentUsers)
                .Include(d => d.DepartmentDocumentTypes)
                .Include(d => d.ComplianceStatuses)
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);

            if (department == null)
                return NotFound(new { error = "Department not found." });

            if (department.Children.Any() || department.DepartmentUsers.Any() || department.DepartmentDocumentTypes.Any() || department.ComplianceStatuses.Any())
                return Conflict(new
                {
                    error = "Department cannot be deleted because it still has child departments, users, document assignments, or compliance records."
                });

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return NoContent();
        }


        // Only the Super Admin manages departments; Department Admins cannot edit or delete them.
        [Authorize(Policy = "SuperAdminOnly")]
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

        // Only the Super Admin manages departments; Department Admins cannot edit or delete them.
        [Authorize(Policy = "SuperAdminOnly")]
        [HttpDelete]
        [Route("{DepartmentId}")]
        public async Task<IActionResult> Delete([FromRoute] int DepartmentId)
        {
            return await this.SafeDeleteAsync(
                "Department",
                DepartmentId.ToString(),
                async () =>
                {
                    var departmentModel = await _context.Departments
                        .Include(d => d.Children)
                        .Include(d => d.DepartmentUsers)
                        .Include(d => d.DepartmentDocumentTypes)
                        .Include(d => d.ComplianceStatuses)
                        .FirstOrDefaultAsync(x => x.DepartmentId == DepartmentId);

                    if (departmentModel == null)
                        throw new KeyNotFoundException($"Department with ID {DepartmentId} not found");

                    if (departmentModel.Children.Any() || departmentModel.DepartmentUsers.Any() || departmentModel.DepartmentDocumentTypes.Any() || departmentModel.ComplianceStatuses.Any())
                        throw new DeletionConflictException(
                            "Department",
                            DepartmentId.ToString(),
                            new List<string>
                            {
                                departmentModel.Children.Any() ? "Child Departments" : string.Empty,
                                departmentModel.DepartmentUsers.Any() ? "Users" : string.Empty,
                                departmentModel.DepartmentDocumentTypes.Any() ? "Document Type Assignments" : string.Empty,
                                departmentModel.ComplianceStatuses.Any() ? "Compliance Records" : string.Empty
                            }.Where(x => !string.IsNullOrEmpty(x)).ToList()
                        );

                    _context.Departments.Remove(departmentModel);
                    await _context.SaveChangesAsync();
                }
            );
        }

        private static bool IsDescendantOf(int departmentId, int potentialParentId, IEnumerable<Department> allDepartments)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(potentialParentId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!visited.Add(currentId))
                    continue;

                if (currentId == departmentId)
                    return true;

                var children = allDepartments
                    .Where(d => d.ParentId == currentId)
                    .Select(d => d.DepartmentId)
                    .ToList();

                foreach (var childId in children)
                    queue.Enqueue(childId);
            }

            return false;
        }

        private static DepartmentHierarchyNodeDto BuildHierarchyNode(Department department, IEnumerable<Department> allDepartments)
        {
            var childNodes = allDepartments
                .Where(d => d.ParentId == department.DepartmentId)
                .OrderBy(d => d.DepartmentName)
                .Select(d => BuildHierarchyNode(d, allDepartments))
                .ToList();

            return new DepartmentHierarchyNodeDto
            {
                DepartmentId = department.DepartmentId,
                ParentId = department.ParentId,
                DepartmentName = department.DepartmentName,
                BranchId = department.BranchId,
                Children = childNodes
            };
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

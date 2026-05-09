using FourierIT_API.Data;
using FourierIT_API.DTOs.Role;
using FourierIT_API.DTOs.User;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/roles")]
    [ApiController]
    //Protect this controller with an admin role as appropriate
    [Authorize(Roles = "Department Admin")]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _context;

        public RolesController(RoleManager<Role> roleManager, UserManager<User> userManager, AppDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _roleManager.Roles.Select(r => new RoleDto { RoleId = r.Id, RoleName = r.Name ?? string.Empty })
                        .ToListAsync();
            return Ok(roles);
        }

        [HttpGet("{RoleId}")]
        public async Task<IActionResult> GetByRoleId([FromRoute] string RoleId)
        {
            var role = await _roleManager.FindByIdAsync(RoleId);
            if (role == null) return NotFound();

            return Ok(new RoleDto { RoleId = role.Id, RoleName = role.Name ?? string.Empty });
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequestDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.RoleName))
                return BadRequest(new { error = "Role name is required" });

            var roleName = model.RoleName.Trim();
            if(await _roleManager.RoleExistsAsync(roleName)) return Conflict (new { error = "Role already exists" });

            // Validate optional provided Id
            if (!string.IsNullOrWhiteSpace(model.RoleId))
            {
                var providedId = model.RoleId!.Trim();

                // Basic validations: no spaces, reasonable length
                if (providedId.Contains(" ") || providedId.Length > 450)
                    return BadRequest(new { error = "Provided Id is invalid (no spaces, max length 450)." });

                // Ensure Id not already used
                var existingById = await _roleManager.FindByIdAsync(providedId);
                if (existingById != null)
                    return Conflict(new { error = "Provided Id already in use.", id = providedId });

                var roleWithName = await _roleManager.FindByNameAsync(roleName);
                if (roleWithName != null)
                    return Conflict(new { error = "Role name already exists." });

                var roleWithProvidedId = new Role
                {
                    Id = providedId,
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };

                var createResult = await _roleManager.CreateAsync(roleWithProvidedId);
                if (!createResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, createResult.Errors);

                return CreatedAtAction(nameof(GetByRoleId), new { RoleId = roleWithProvidedId.Id }, new RoleDto { RoleId = roleWithProvidedId.Id, RoleName = roleWithProvidedId.Name ?? string.Empty });
            }
            else
            {
                // Let Identity assign the Id
                var role = new Role
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };
                var result = await _roleManager.CreateAsync(role);
                if (!result.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);

                return CreatedAtAction(nameof(GetByRoleId), new { RoleId = role.Id }, new RoleDto { RoleId = role.Id, RoleName = role.Name ?? string.Empty });
            }
        }

        [HttpPut("{RoleId}")]
        public async Task<IActionResult> UpdateRole([FromRoute] string RoleId, [FromBody] UpdateRoleRequestDto  model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.RoleName)) return BadRequest(new { error = "Role name is required." });

            var oldRole = await _roleManager.FindByIdAsync(RoleId);
            if (oldRole == null) return NotFound();

            var newName = model.RoleName.Trim();

            // Name conflict check
            var existingByName = await _roleManager.FindByNameAsync(newName);
            if (existingByName != null && existingByName.Id != RoleId) return Conflict(new { error = "Another role with that name already exists." });

            // If no NewId requested, just update name
            if (string.IsNullOrWhiteSpace(model.NewRoleId) || model.NewRoleId.Trim() == RoleId)
            {
                oldRole.Name = newName;
                oldRole.NormalizedName = newName.ToUpperInvariant();

                var updateRes = await _roleManager.UpdateAsync(oldRole);
                if (!updateRes.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, updateRes.Errors);

                return Ok(new RoleDto { RoleId = oldRole.Id, RoleName = oldRole.Name ?? string.Empty });
            }

            // NewId provided and different -> perform safe id-change by creating a new role, repointing FKs, deleting old role
            var newId = model.NewRoleId.Trim();
            if (newId.Contains(" ") || newId.Length > 4) return BadRequest(new { error = "Provided NewId is invalid (no spaces, max length 450)." });

            var existingById = await _roleManager.FindByIdAsync(newId);
            if (existingById != null) return Conflict(new { error = "Target Id already exists.", id = newId });

            // Create new role with requested id and target name
            var newRole = new Role
            {
                Id = newId,
                Name = newName,
                NormalizedName = newName.ToUpperInvariant()
            };

            var createNewRes = await _roleManager.CreateAsync(newRole);
            if (!createNewRes.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, createNewRes.Errors);

            // Repoint join tables to the new role id
            try
            {
                // Update UserRole FK rows
                var userRoles = await _context.UserRoles
                    .Where(ur => ur.RoleId == RoleId)
                    .ToListAsync();

                foreach (var ur in userRoles)
                {
                    ur.RoleId = newId;
                    _context.UserRoles.Update(ur);
                }

                // Update RolePermission FK rows
                var rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == RoleId)
                    .ToListAsync();

                foreach (var rp in rolePermissions)
                {
                    rp.RoleId = newId;
                    _context.RolePermissions.Update(rp);
                }

                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                // Attempt rollback: delete the newly created role
                await _roleManager.DeleteAsync(newRole);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to reassign relationships to new role id.", detail = ex.Message });
            }

            // Delete old role
            var deleteOld = await _roleManager.DeleteAsync(oldRole);
            if (!deleteOld.Succeeded)
            {
                // Attempt to roll back relationship changes (best-effort): set FKs back to old id and re-create old role
                // Recreate old role
                var recreateOld = new Role { Id = RoleId, Name = oldRole.Name, NormalizedName = oldRole.NormalizedName };
                var recreateRes = await _roleManager.CreateAsync(recreateOld);

                // best-effort restore FKs
                var affectedUserRoles = await _context.UserRoles.Where(ur => ur.RoleId == newId && ur.RoleId == null).ToListAsync();
                foreach (var ur in affectedUserRoles) ur.RoleId = RoleId;
                var affectedRolePerms = await _context.RolePermissions.Where(rp => rp.RoleId == newId && rp.Role == null).ToListAsync();
                foreach (var rp in affectedRolePerms) rp.RoleId = RoleId;
                await _context.SaveChangesAsync();

                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to delete old role after id-change.", deleteErrors = deleteOld.Errors, recreateOldSucceeded = recreateRes.Succeeded });
            }
            return Ok(new RoleDto { RoleId = newRole.Id, RoleName = newRole.Name ?? string.Empty });
        }

        [HttpDelete("{RoleId}")]
        public async Task<IActionResult> DeleteRole([FromRoute] string RoleId)
        {
            var role = await _roleManager.FindByIdAsync(RoleId);
            if (role == null) return NotFound();

            // Prevent deleting a role that still has users assigned
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name ?? string.Empty);
            if (usersInRole.Any()) return BadRequest(new { error = "Role has assigned users. Remove users from the role before deleting" });

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);

            return NoContent();
        }
    }
}

using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Models;
using FourierIT_API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Controllers
{
    [Route("api/AssignRole")]
    [ApiController]
    public class AssignRoleController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly IConfiguration _configuration;
        public AssignRoleController(AppDbContext context, UserManager<User> userManager, RoleManager<Role> roleManager, IConfiguration configuration)
        {
            //this is assigning the variables 
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }
        [HttpGet("{userName}/Roles")]
        public async Task<IActionResult> GetUserRoles([FromRoute] string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return BadRequest(new { error = "UserName required." });

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound(new { error = "User not found." });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { user = user.UserName, roles });
        }

        // POST: assign a role to a user (body allowed)
        [HttpPost("{userName}/roles")]
        public async Task<IActionResult> AssignRoleToUser([FromRoute] string userName, [FromBody] string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return BadRequest(new { error = "Role name is required." });

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound(new { error = "User not found." });
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be modified." });

            roleName = roleName.Trim();

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var allowedRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
                return BadRequest(new { error = "Role does not exist.", allowedRoles });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var addError = StakeholderRolePolicy.ValidateAddRole(currentRoles, roleName);
            if (addError != null)
                return BadRequest(new { error = addError });

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { user = user.UserName, roles });
        }

        [HttpPut("{userName}/Roles/replace")]
        public async Task<IActionResult> ReplaceUserRole([FromRoute] string userName, [FromBody] ReplaceRoleDto dto)
        {
            if (dto == null) return BadRequest("Request body required");
            if (string.IsNullOrEmpty(dto.OldRole) || string.IsNullOrEmpty(dto.NewRole)) return BadRequest("Both the old and new role names must be provided");

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound();
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be modified." });

            var oldRole = dto.OldRole.Trim();
            var newRole = dto.NewRole.Trim();

            if (!await _roleManager.RoleExistsAsync(oldRole)) return BadRequest(new { error = "Old role does not exist", role = oldRole });
            if (!await _roleManager.RoleExistsAsync(newRole)) return BadRequest(new { error = "New role does not exist", role = newRole });

            if (!await _userManager.IsInRoleAsync(user, oldRole)) return BadRequest(new { error = "User is not in the old role", role = oldRole });

            var rolesBeforeReplace = await _userManager.GetRolesAsync(user);
            var replaceError = StakeholderRolePolicy.ValidateReplaceRole(rolesBeforeReplace, oldRole, newRole);
            if (replaceError != null)
                return BadRequest(new { error = replaceError });

            // Attempt replace, with rollback if add fails
            var removedResult = await _userManager.RemoveFromRoleAsync(user, oldRole);
            if (!removedResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, removedResult.Errors);

            var addedResult = await _userManager.AddToRoleAsync(user, newRole);
            if (!addedResult.Succeeded)
            {
                // rollback
                var rollbackResult = await _userManager.AddToRoleAsync(user, oldRole);
                if (!rollbackResult.Succeeded)
                {
                    // Both operations failed — surface both error sets
                    return StatusCode(StatusCodes.Status500InternalServerError, new { addErrors = addedResult.Errors, rollbackErrors = rollbackResult.Errors });
                }

                return StatusCode(StatusCodes.Status500InternalServerError, addedResult.Errors);
            }

            var updatedRoles = await _userManager.GetRolesAsync(user);
            return Ok(new { userName = user.UserName, roles = updatedRoles });
        }

        //Remove specific role from a user
        [HttpDelete("{userName}/roles/remove")]
        public async Task<IActionResult> RemoveUserRole([FromRoute] string userName, [FromBody] string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return BadRequest(new { error = "Role name is required." });

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound();
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be modified." });

            roleName = roleName.Trim();

            if (!await _roleManager.RoleExistsAsync(roleName)) return BadRequest(new { error = "Role does not exist", roleName });

            var isInRole = await _userManager.IsInRoleAsync(user, roleName);
            if (!isInRole) return BadRequest(new { error = "User is not in the specified role", roleName });

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);

            return Ok(new { userName = user.UserName, roles = await _userManager.GetRolesAsync(user) });
        }
        private bool IsSuperAdminUser(User? user)
        {
            if (user == null) return false;
            var superUserName = _configuration["SuperAdmin:Username"] ?? "superadmin";
            return string.Equals(user.UserName, superUserName, StringComparison.OrdinalIgnoreCase);
        }
    }
}

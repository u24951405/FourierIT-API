using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System.Linq.Expressions;

namespace FourierIT_API.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase // Inherits from ControllerBase, which provides basic functionalities for handling HTTP requests and responses in an API controller.
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<User> _signInManager;
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        public UserController(UserManager<User> userManager, ITokenService tokenService, SignInManager<User> signInManager, AppDbContext context, RoleManager<IdentityRole> roleManager) // constructor for the ProfileController class, which takes an AppDbContext instance as a parameter. This allows for dependency injection of the database context when the controller is instantiated.
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            if (!ModelState.IsValid)

                return BadRequest(ModelState);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Username.ToLower());

            if (user == null) return Unauthorized("Invalid username");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized("Username not found and/or password incorrect");

            return Ok(
                new NewUserDto
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    Token = _tokenService.CreateToken(user)
                });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var User = new User
                {
                    UserName = userDto.Username?.ToLower(),
                    Email = userDto.EmailAddress,
                    PhoneNumber = userDto.PhoneNumber,
                    AccountStatus = "Active"
                };

                var createdUser = await _userManager.CreateAsync(User, userDto.Password);

                if (createdUser.Succeeded)
                {
                    // User role from DTO, it will default to owner if null/empty
                    var roleName = string.IsNullOrEmpty(userDto.Role) ? "Document Owner" : userDto.Role.Trim();

                    // Validate role against the seeded roles
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                      //Return a helpful error listing the available roles instead of creating arbitrary roles
                      var allowedRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
                        return BadRequest(new { error = "Invalid role", allowedRoles });
                    }
                    var roleResult = await _userManager.AddToRoleAsync(User, roleName);

                    if (!roleResult.Succeeded)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, roleResult.Errors);
                    }

                    var profile = new Profile
                    {
                        FirstName = userDto.FirstName ?? string.Empty,
                        LastName = userDto.LastName ?? string.Empty,
                        DateOfBirth = userDto.DateOfBirth,
                        PhoneNumber = userDto.PhoneNumber ?? string.Empty,
                        JobTitle = userDto.JobTitle ?? string.Empty,
                        UserId = User.Id
                    };

                    _context.Profiles.Add(profile);
                    await _context.SaveChangesAsync();

                    return Ok(
                        new NewUserDto
                        {
                            UserName = User.UserName,
                            Email = User.Email,
                            Token = _tokenService.CreateToken(User)
                        }
                    );
                }
                else
                {
                    return BadRequest(createdUser.Errors);
                }
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "Registration failed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{userName}/Roles")]
        public async Task<IActionResult> AssignRoleToUser([FromRoute] string userName, [FromBody] string roleName)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound();

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createRoleResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, createRoleResult.Errors);
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok();
        }

        [HttpPut("{userName}/Roles/replace")]
        public async  Task<IActionResult> ReplaceUserRole([FromRoute] string userName, [FromBody] ReplaceRoleDto dto)
        {
            if (dto == null!) return BadRequest("Request body required");
            if (string.IsNullOrEmpty(dto.OldRole) || string.IsNullOrEmpty(dto.NewRole))
            return BadRequest("Both the old and new role names must be provided");

             var user = await _userManager.FindByNameAsync(userName);

            if (user == null) return NotFound();

            //validate that the roles exist
            if (!await _roleManager.RoleExistsAsync(dto.OldRole))
                return BadRequest(new { error = "Old role does not exist", role = dto.OldRole });

            if (!await _roleManager.RoleExistsAsync(dto.NewRole))
                return BadRequest(new { error = "New role does not exist", role = dto.NewRole });

            // Ensure user currently has the old role
            if (!await _userManager.IsInRoleAsync(user, dto.OldRole))
                return BadRequest(new { error = "User is not in the old role", role = dto.OldRole });

            //Remove old role
            var removedResult = await _userManager.RemoveFromRoleAsync(user, dto.OldRole);
            if (!removedResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, removedResult.Errors);

            //Add new role
            var addedResult = await _userManager.AddToRoleAsync(user, dto.NewRole);
            if (!addedResult.Succeeded)
            {
                // Attempt to rollback to old role if adding new role fails
                await _userManager.AddToRoleAsync(user, dto.OldRole);
                return StatusCode(StatusCodes.Status500InternalServerError, addedResult.Errors);
            }

            var updatedRoles = await _userManager.GetRolesAsync(user);
            return Ok(new { userName = user.UserName, roles = updatedRoles });
        }

        //Remove specific role from a user
        [HttpDelete("{userName}/roles/remove")]
        public async Task<IActionResult> RemoveUserRole([FromRoute] string userName, [FromBody] string roleName)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound();

            var existingRole = await _roleManager.RoleExistsAsync(roleName);

            if (!existingRole)
            {
                return BadRequest(new { error = "Role does not exist", roleName });
            }

            var isInRole = await _userManager.IsInRoleAsync(user, roleName);
            if (!isInRole) return BadRequest(new { error = "User is not in the specified role", roleName });

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if(!result.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);

            return Ok(new {userName = user.UserName, roles = await _userManager.GetRolesAsync(user)});
        }
    }
}

using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Linq.Expressions;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace FourierIT_API.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<User> _signInManager;
        private readonly AppDbContext _context;
        private readonly RoleManager<Role> _roleManager;

        public UserController(UserManager<User> userManager, ITokenService tokenService, SignInManager<User> signInManager, AppDbContext context, RoleManager<Role> roleManager)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)//This login method first checks if the user’s input is valid, then looks for the user in the database, verifies that the password is correct, creates a login token if everything matches, and finally returns the user’s details and token so they can access the system
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Username.ToLower());

            if (user == null) return Unauthorized("Invalid username");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized("Username not found and/or password incorrect");

            var token = await _tokenService.CreateTokenAsync(user);

            return Ok(
                new NewUserDto
                {
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Token = token
                });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentAccount()
        {
            // JWT short names (e.g. "email", "sub") are often not mapped to ClaimTypes.* unless MapInboundClaims is configured.
            var user = await ResolveCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { error = "Invalid token." });

            var roles = (await _userManager.GetRolesAsync(user)).ToList();
            var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);

            var dto = new CurrentAccountDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                AccountStatus = user.AccountStatus,
                PhoneNumber = user.PhoneNumber,
                Roles = roles,
                ProfileId = profile?.ProfileId,
                FirstName = profile?.FirstName,
                LastName = profile?.LastName,
                ProfilePhoneNumber = profile?.PhoneNumber,
                JobTitle = profile?.JobTitle,
                DateOfBirth = profile?.DateOfBirth
            };

            return Ok(dto);
        }

        private async Task<User?> ResolveCurrentUserAsync()
        {
            var email =
                User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.Claims.FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(email))
            {
                // Trim email to handle cases where email has leading/trailing whitespace
                var trimmedEmail = email.Trim();
                var byEmail = await _userManager.FindByEmailAsync(trimmedEmail);
                if (byEmail != null) return byEmail;
            }

            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.Claims.FirstOrDefault(c => c.Type.Equals("sub", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
                return await _userManager.FindByIdAsync(userId);

            return null;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                // Build requested roles early so we can decide entity assignment and validate roles before creating the user
                var requestedRoles = (userDto.Roles ?? new List<string>())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Backward compatibility for older clients sending a single role.
                if (requestedRoles.Count == 0 && !string.IsNullOrWhiteSpace(userDto.Role))
                {
                    requestedRoles.Add(userDto.Role.Trim());
                }

                if (requestedRoles.Count == 0)
                {
                    requestedRoles.Add("Document Owner");
                }

                if (requestedRoles.Count > 2)
                {
                    return BadRequest(new { error = "A user can be assigned a maximum of 2 roles during registration." });
                }

                // Validate roles exist in DB and stakeholder exclusivity BEFORE creating the user
                var allowedRoleNames = await _context.Roles
                    .Select(r => r.Name ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToListAsync();

                var invalidRoles = requestedRoles
                    .Where(r => !allowedRoleNames.Any(ar => ar.Equals(r, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (invalidRoles.Any())
                {
                    return BadRequest(new { error = "Invalid role(s)", invalidRoles, allowedRoles = allowedRoleNames });
                }

                if (StakeholderRolePolicy.ViolatesStakeholderExclusivity(requestedRoles))
                {
                    return BadRequest(new
                    {
                        error = "Stakeholder cannot be combined with other roles. Choose only Stakeholder, or remove Stakeholder and pick other role(s)."
                    });
                }

                // Determine entity assignment based on requested roles
                int? entityTypeIdToAssign = null;
                var isDocumentOwner = requestedRoles.Any(r => r.Equals("Document Owner", StringComparison.OrdinalIgnoreCase));
                var isDepartmentAdmin = requestedRoles.Any(r => r.Equals("Department Admin", StringComparison.OrdinalIgnoreCase));

                if (isDocumentOwner && !isDepartmentAdmin)
                {
                    var individualEntity = await _context.EntityTypes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(et => et.Name != null && et.Name.ToLower().Contains("individual"));
                    if (individualEntity != null) entityTypeIdToAssign = individualEntity.EntityTypeId;
                }
                else if (isDepartmentAdmin && !isDocumentOwner)
                {
                    var departmentEntity = await _context.EntityTypes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(et => et.Name != null && et.Name.ToLower().Contains("department"));

                    if (departmentEntity == null)
                    {
                        departmentEntity = await _context.EntityTypes
                            .AsNoTracking()
                            .FirstOrDefaultAsync(et => et.Name != null && (
                                et.Name.ToLower().Contains("institution")
                                || et.Name.ToLower().Contains("company")
                                || et.Name.ToLower().Contains("organisation")
                                || et.Name.ToLower().Contains("organization")));
                    }

                    if (departmentEntity != null) entityTypeIdToAssign = departmentEntity.EntityTypeId;
                }
                // If both roles are present, leave EntityTypeId null and force selection at upload time.

                var newUser = new User
                {
                    UserName = userDto.Username?.ToLower(),
                    Email = userDto.EmailAddress?.Trim(),
                    PhoneNumber = userDto.PhoneNumber,
                    AccountStatus = "Active",
                    EntityTypeId = entityTypeIdToAssign
                };

                var createdUser = await _userManager.CreateAsync(newUser, userDto.Password);
                if (!createdUser.Succeeded)
                {
                    return BadRequest(createdUser.Errors);
                }

                // Add roles (roles already validated)
                var roleResult = await _userManager.AddToRolesAsync(newUser, requestedRoles);
                if (!roleResult.Succeeded)
                {
                    // Attempt cleanup: delete partially created user
                    await _userManager.DeleteAsync(newUser);
                    return StatusCode(StatusCodes.Status500InternalServerError, roleResult.Errors);
                }

                var profile = new Profile
                {
                    FirstName = userDto.FirstName ?? string.Empty,
                    LastName = userDto.LastName ?? string.Empty,
                    DateOfBirth = userDto.DateOfBirth,
                    PhoneNumber = userDto.PhoneNumber ?? string.Empty,
                    JobTitle = userDto.JobTitle ?? string.Empty,
                    UserId = newUser.Id
                };

                _context.Profiles.Add(profile);
                await _context.SaveChangesAsync();

                var token = await _tokenService.CreateTokenAsync(newUser);

                return Ok(
                    new NewUserDto
                    {
                        UserName = newUser.UserName ?? string.Empty,
                        Email = newUser.Email ?? string.Empty,
                        Token = token
                    }
                );
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "Registration failed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [Authorize(Roles = "Department Admin")]
        [HttpPut("profile/{profileId}")]
        public async Task<IActionResult> UpdateManagedUser([FromRoute] int profileId, [FromBody] UpdateUserManagementRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var profile = await _context.Profiles.Include(p => p.User).FirstOrDefaultAsync(p => p.ProfileId == profileId);
            if (profile == null) return NotFound(new { error = "Profile not found." });

            var user = profile.User ?? await _userManager.FindByIdAsync(profile.UserId);
            if (user == null) return NotFound(new { error = "User not found for the profile." });

            var roleName = dto.Role.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var allowedRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
                return BadRequest(new { error = "Invalid role", allowedRoles });
            }

            profile.FirstName = dto.FirstName.Trim();
            profile.LastName = dto.LastName.Trim();
            profile.DateOfBirth = dto.DateOfBirth;
            profile.PhoneNumber = dto.PhoneNumber.Trim();
            profile.JobTitle = dto.JobTitle.Trim();

            user.Email = dto.EmailAddress.Trim();
            user.NormalizedEmail = dto.EmailAddress.Trim().ToUpperInvariant();
            user.AccountStatus = string.IsNullOrWhiteSpace(dto.AccountStatus) ? "Active" : dto.AccountStatus.Trim();
            var userUpdateResult = await _userManager.UpdateAsync(user);
            if (!userUpdateResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, userUpdateResult.Errors);

            var existingRoles = await _userManager.GetRolesAsync(user);
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!removeRolesResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, removeRolesResult.Errors);

            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, addRoleResult.Errors);

            await _context.SaveChangesAsync();
            return Ok(new { message = "User and profile updated." });
        }

        [Authorize(Roles = "Department Admin")]
        [HttpDelete("profile/{profileId}")]
        public async Task<IActionResult> DeleteManagedUser([FromRoute] int profileId)
        {
            var profile = await _context.Profiles.Include(p => p.User).FirstOrDefaultAsync(p => p.ProfileId == profileId);
            if (profile == null) return NotFound(new { error = "Profile not found." });

            var user = profile.User ?? await _userManager.FindByIdAsync(profile.UserId);
            if (user == null) return NotFound(new { error = "User not found for the profile." });

            _context.Profiles.Remove(profile);
            await _context.SaveChangesAsync();

            var deleteUserResult = await _userManager.DeleteAsync(user);
            if (!deleteUserResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, deleteUserResult.Errors);

            return NoContent();
        }

        [Authorize(Roles = "Department Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .ToListAsync();

            var userDtos = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var profile = await _context.Profiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                userDtos.Add(new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.PhoneNumber,
                    user.AccountStatus,
                    Roles = roles,
                    Profile = new
                    {
                        profile?.FirstName,
                        profile?.LastName,
                        profile?.JobTitle,
                        profile?.DateOfBirth
                    }
                });
            }

            return Ok(userDtos);
        }
    }
}


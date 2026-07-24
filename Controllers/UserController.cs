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
using Microsoft.Extensions.Configuration;
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
        private readonly IEntityVerificationService _entityVerificationService;
        private readonly IConfiguration _configuration;

        public UserController(
            UserManager<User> userManager,
            ITokenService tokenService,
            SignInManager<User> signInManager,
            AppDbContext context,
            RoleManager<Role> roleManager,
            IEntityVerificationService entityVerificationService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
            _entityVerificationService = entityVerificationService;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)//This login method first checks if the user’s input is valid, then looks for the user in the database, verifies that the password is correct, creates a login token if everything matches, and finally returns the user’s details and token so they can access the system
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = loginDto.Username?.Trim() ?? string.Empty;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                // Fall back to searching by normalized username if direct lookup fails.
                user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.NormalizedUserName == username.ToUpperInvariant());
            }

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

                var entityType = await _context.EntityTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(et => et.EntityTypeId == userDto.EntityTypeId);

                if (entityType == null)
                {
                    return BadRequest(new { error = "Please select a valid entity type." });
                }

                var verificationNumber = userDto.EntityIdentificationNumber?.Trim();
                if (string.IsNullOrWhiteSpace(verificationNumber))
                {
                    return BadRequest(new { error = "Please enter an identification or registration number for the selected entity type." });
                }

                var expectedLengthMessage = ValidateEntityIdentificationNumber(userDto.EntityTypeId, verificationNumber);
                if (expectedLengthMessage != null)
                {
                    return BadRequest(new { error = expectedLengthMessage });
                }

                var verificationResult = await _entityVerificationService.VerifyEntityAsync(userDto.EntityTypeId, verificationNumber);
                if (!verificationResult.IsValid)
                {
                    return BadRequest(new { error = verificationResult.ErrorMessage ?? "Entity verification failed." });
                }

                var newUser = new User
                {
                    UserName = userDto.Username?.ToLower(),
                    Email = userDto.EmailAddress?.Trim(),
                    PhoneNumber = userDto.PhoneNumber,
                    AccountStatus = "Active",
                    EntityTypeId = userDto.EntityTypeId,
                    EntityIdentificationNumber = verificationNumber
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

        [AllowAnonymous]
        [HttpPost("verify-entity")]
        public async Task<IActionResult> VerifyEntity([FromBody] VerifyEntityRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _entityVerificationService.VerifyEntityAsync(request.EntityTypeId, request.IdentificationNumber?.Trim() ?? string.Empty);
            return Ok(new
            {
                isValid = result.IsValid,
                message = result.ErrorMessage,
                providerUnavailable = result.ProviderUnavailable
            });
        }

        [AllowAnonymous]
        [HttpGet("entity-types")]
        public async Task<IActionResult> GetEntityTypes()
        {
            var entityTypes = await _context.EntityTypes
                .AsNoTracking()
                .OrderBy(et => et.EntityTypeId)
                .Select(et => new
                {
                    et.EntityTypeId,
                    et.Name
                })
                .ToListAsync();

            return Ok(entityTypes);
        }

        private bool IsSuperAdminUser(User? user)
        {
            if (user == null) return false;
            var superUserName = _configuration["SuperAdmin:Username"] ?? "superadmin";
            return string.Equals(user.UserName, superUserName, StringComparison.OrdinalIgnoreCase);
        }

        private static string? ValidateEntityIdentificationNumber(int entityTypeId, string verificationNumber)
        {
            return entityTypeId switch
            {
                1 when !verificationNumber.All(char.IsDigit) || verificationNumber.Length != 13 => "South African ID numbers must be exactly 13 digits.",
                2 when verificationNumber.Length < 4 => "Passport numbers must be at least 4 characters.",
                3 when verificationNumber.Length < 4 => "Company registration numbers must be at least 4 characters.",
                4 when verificationNumber.Length < 4 => "Trust registration numbers must be at least 4 characters.",
                5 when verificationNumber.Length < 4 => "Partnership registration numbers must be at least 4 characters.",
                6 when verificationNumber.Length < 4 => "Please enter a reference number for this entity type.",
                _ => null
            };
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
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be edited." });

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
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be deleted." });

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
                    ProfileId = profile?.ProfileId,
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


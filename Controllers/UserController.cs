using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Linq.Expressions;

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

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var newUser = new User
                {
                    UserName = userDto.Username?.ToLower(),
                    Email = userDto.EmailAddress,
                    PhoneNumber = userDto.PhoneNumber,
                    AccountStatus = "Active"
                };

                var createdUser = await _userManager.CreateAsync(newUser, userDto.Password);

                if (createdUser.Succeeded)
                {
                    var roleName = string.IsNullOrEmpty(userDto.Role) ? "Document Owner" : userDto.Role.Trim();

                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        var allowedRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
                        return BadRequest(new { error = "Invalid role", allowedRoles });
                    }

                    var roleResult = await _userManager.AddToRoleAsync(newUser, roleName);
                    if (!roleResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, roleResult.Errors);

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
    }
}

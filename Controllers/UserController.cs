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
                    //Ensure the "Document Owner" role exists
                    const string defaultRole = "Document Owner";
                    if (!await _roleManager.RoleExistsAsync(defaultRole))
                    {
                        var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(defaultRole));
                        if (!createRoleResult.Succeeded)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, createRoleResult.Errors);
                        }
                    }
                    var roleResult = await _userManager.AddToRoleAsync(User, defaultRole);

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

        [HttpPost("{userId}/Roles")]
        public async Task<IActionResult> AssignRoleToUser([FromRoute] string userId, [FromBody] string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
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
    }
}

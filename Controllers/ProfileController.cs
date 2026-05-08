using FourierIT_API.Data;
using FourierIT_API.DTOs.Profile;
using FourierIT_API.Interfaces;
using FourierIT_API.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using FourierIT_API.Models;
using System.Transactions;

namespace FourierIT_API.Controllers
{
    [Route("api/Profile")]
    [ApiController]
    public class ProfileController : ControllerBase // Inherits from ControllerBase, which provides basic functionalities for handling HTTP requests and responses in an API controller.
    {
        private readonly AppDbContext _context; // Declares a private readonly field of type AppDbContext, which is used to interact with the database.
        private readonly IProfileRepository _profileRepo;
        private readonly UserManager<User> _userManager;
        public ProfileController(AppDbContext context, IProfileRepository profileRepo, UserManager<User> userManager) // constructor for the ProfileController class, which takes an AppDbContext instance as a parameter. This allows for dependency injection of the database context when the controller is instantiated.
        {
            _profileRepo = profileRepo;
            _context = context; // Initializes the _context field with the provided AppDbContext instance.
            _userManager = userManager;
        }

        [HttpGet] // same as read
        // code that will grab it from the database
        public async Task<IActionResult> GetAllProfiles()
        {
            // Retrieves all profiles from the database using the _context and converts them to a list. Deferred execution is when the query is not executed until the data is actually needed, which can improve performance by allowing for optimizations and reducing unnecessary database calls.
            var profiles = await _profileRepo.GetAllProfilesAsync();
            
            var profileDto = profiles.Select(p => p.ToProfileDto()); // return an immutable array of the dto

            return Ok(profileDto); // Returns an HTTP 200 OK response with the list of profiles as the response body.
        }

        [HttpGet("{profileId}")] // takes in variable id, so we get one record at a time
        public async Task<IActionResult> GetByProfileId([FromRoute] int profileId) // IActionResult is a common return method for API controllers, allowing for flexibility in the type of response returned (e.g., Ok, NotFound, BadRequest, etc.). The GetProfile method takes an integer id as a parameter, which is used to identify the specific profile to retrieve from the database.
        {
            var profile = await _profileRepo.GetByProfileIdAsync(profileId); // Uses the Find method of the _context to search for a profile with the specified id. This method is efficient for retrieving entities by their primary key.

            // null check
            if (profile == null) // Checks if the retrieved profile is null, which indicates that no profile with the specified id was found in the database.
            {
                return NotFound(); // If the profile is null, returns an HTTP 404 Not Found response.
            }

            return Ok(profile.ToProfileDto());
        }


        [HttpPut]
        [Route("{profileId}")]
        public async Task<IActionResult> UpdateProfile([FromRoute] int profileId, [FromBody] UpdateProfileRequestDto updateDto)
        {
            // use a searching algorithm to find profile, this retrieves the data
            var profileModel = await _profileRepo.UpdateProfileAsync(profileId, updateDto);

            if(profileModel == null)
            {
                return NotFound();
            }

            return Ok(profileModel.ToProfileDto());           
        }

        // entity framework does the deleting we just need to find the id
        [HttpDelete]
        [Route("{profileId}")]
        public async Task<IActionResult> DeleteProfile([FromRoute] int profileId)
        {
            var profileModel = await _profileRepo.GetByProfileIdAsync(profileId);

            if (profileModel == null)
            {
                return NotFound();
            }

            // Ensure both profile and related user are deleted in a single transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Attempt to find the related user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == profileModel.UserId);

                if (user != null)
                {
                    _context.Users.Remove(user);
                }

                _context.Profiles.Remove(profileModel);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Problem(detail: ex.Message, title: "Deletion failed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}

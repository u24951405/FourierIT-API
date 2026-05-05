using FourierIT_API.Data;
using FourierIT_API.DTOs.Profile;
using FourierIT_API.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FourierIT_API.Controllers
{
    [Route("api/Profile")]
    [ApiController]
    public class ProfileController : ControllerBase // Inherits from ControllerBase, which provides basic functionalities for handling HTTP requests and responses in an API controller.
    {
        private readonly AppDbContext _context; // Declares a private readonly field of type AppDbContext, which is used to interact with the database.
        public ProfileController(AppDbContext context) // constructor for the ProfileController class, which takes an AppDbContext instance as a parameter. This allows for dependency injection of the database context when the controller is instantiated.
        {
            _context = context; // Initializes the _context field with the provided AppDbContext instance.
        }

        [HttpGet] // same as read
        // code that will grab it from the database
        public IActionResult GetAllProfiles()
        {
            // Retrieves all profiles from the database using the _context and converts them to a list. Deferred execution is when the query is not executed until the data is actually needed, which can improve performance by allowing for optimizations and reducing unnecessary database calls.
            var profiles = _context.Profiles.ToList()
                .Select(p => p.ToProfileDto()); // return an immutable array of the dto

            return Ok(profiles); // Returns an HTTP 200 OK response with the list of profiles as the response body.
        }

        [HttpGet("{ProfileId}")] // takes in variable id, so we get one record at a time
        public IActionResult GetByProfileId([FromRoute] int ProfileId) // IActionResult is a common return method for API controllers, allowing for flexibility in the type of response returned (e.g., Ok, NotFound, BadRequest, etc.). The GetProfile method takes an integer id as a parameter, which is used to identify the specific profile to retrieve from the database.
        {
            var Profile = _context.Profiles.Find(ProfileId); // Uses the Find method of the _context to search for a profile with the specified id. This method is efficient for retrieving entities by their primary key.

            // null check
            if (Profile == null) // Checks if the retrieved profile is null, which indicates that no profile with the specified id was found in the database.
            {
                return NotFound(); // If the profile is null, returns an HTTP 404 Not Found response.
            }

            return Ok(Profile.ToProfileDto());
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateProfileRequestDto profileDto) //we need the [frombody] because the data is sent in the form of json, we will be passing it through the body of the http and not the url
        {
            var profileModel = profileDto.ToProfileFromCreateDto(); // Converts the incoming CreateProfileRequest DTO to a Profile entity using the ToProfileFromCreateDto extension method defined in the ProfileMappers class.
            _context.Profiles.Add(profileModel);
            _context.SaveChanges(); // Saves the changes to the database, which will insert the new profile record.
            return CreatedAtAction(nameof(GetByProfileId), new { ProfileId = profileModel.ProfileId }, profileModel.ToProfileDto()); // Returns an HTTP 201 Created response with the location of the newly created profile and the profile data in the response body. The CreatedAtAction method is used to generate a URL for the GetById action, which can be used to retrieve the newly created profile.
        }
    }
}

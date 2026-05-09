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
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace FourierIT_API.Controllers
{
    [Route("api/Profile")]
    [ApiController]
    [Authorize(Roles = "Department Admin")]
    public class ProfileController : ControllerBase 
    {
        private readonly AppDbContext _context; 
        private readonly IProfileRepository _profileRepo;
        private readonly UserManager<User> _userManager;
        public ProfileController(AppDbContext context, IProfileRepository profileRepo, UserManager<User> userManager) 
        {
            _profileRepo = profileRepo;
            _context = context; 
            _userManager = userManager;
        }

        [HttpGet] 
        public async Task<IActionResult> GetAllProfiles()
        {
            var profiles = await _profileRepo.GetAllProfilesAsync();
            var profileDtos = new List<ProfileDto>();

            foreach (var profile in profiles)
            {
                var dto = profile.ToProfileDto();

                var user = profile.User ?? await _userManager.FindByIdAsync(profile.UserId);
                if (user != null)
                {
                    dto.UserName = user.UserName ?? string.Empty; // ensure username is set
                    var roles = await _userManager.GetRolesAsync(user);
                    dto.Role = (roles != null && roles.Any()) ? roles.ToList() : new List<string>();
                }

                profileDtos.Add(dto);
            }

            return Ok(profileDtos);
        }

        [HttpGet("{profileId}")]
        public async Task<IActionResult> GetByProfileId([FromRoute] int profileId)
        {
            var profile = await _profileRepo.GetByProfileIdAsync(profileId);

            if (profile == null)
            {
                return NotFound();
            }

            var dto = profile.ToProfileDto();

            var user = profile.User ?? await _userManager.FindByIdAsync(profile.UserId);
            if (user != null)
            {
                dto.UserName = user.UserName ?? string.Empty; // ensure username is set
                var roles = await _userManager.GetRolesAsync(user);
                dto.Role = (roles != null && roles.Any()) ? roles.ToList() : new List<string>();
            }


            return Ok(dto);
        }
        
        [HttpPut]
        [Route("{profileId}")]
        public async Task<IActionResult> UpdateProfile([FromRoute] int profileId, [FromBody] UpdateProfileRequestDto updateDto)
        {
            var profileModel = await _profileRepo.UpdateProfileAsync(profileId, updateDto);

            if(profileModel == null)
            {
                return NotFound();
            }

            return Ok(profileModel.ToProfileDto());           
        }

        [HttpDelete]
        [Route("{profileId}")]
        public async Task<IActionResult> DeleteProfile([FromRoute] int profileId)
        {
            var profileModel = await _profileRepo.DeleteProfileAsync(profileId);

            if(profileModel == null)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}

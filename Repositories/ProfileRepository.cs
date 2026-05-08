using FourierIT_API.Data;
using FourierIT_API.DTOs.Profile;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Repositories
{
    public class ProfileRepository : IProfileRepository
    {

        private readonly AppDbContext _context;
        // dependency injection
        public ProfileRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<List<Profile>> GetAllProfilesAsync()
        {
            return await _context.Profiles.Include(p => p.User).ToListAsync();
        }

        public async Task<Profile> CreateProfileAsync(Profile profileModel)
        {

            await _context.Profiles.AddAsync(profileModel);
            await _context.SaveChangesAsync();
            return profileModel;
        }

        public async Task<Profile?> DeleteProfileAsync(int profileId)
        {
            var profileModel = await _context.Profiles.FirstOrDefaultAsync(p => p.ProfileId == profileId);

            if (profileModel == null)
            {
                return null;
            }

             _context.Profiles.Remove(profileModel);
            await _context.SaveChangesAsync();
            return profileModel;
        }

        public async Task<Profile?> GetByProfileIdAsync(int profileId)
        {
            return await _context.Profiles.Include(p => p.User).FirstOrDefaultAsync(p => p.ProfileId == profileId);
        }

        public async Task<Profile?> UpdateProfileAsync(int profileId, UpdateProfileRequestDto profileDto)
        {
            var existingProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.ProfileId == profileId);

            if (existingProfile == null)
            {
                return null;
            }

            existingProfile.FirstName = profileDto.FirstName;
            existingProfile.LastName = profileDto.LastName;
            existingProfile.DateOfBirth = profileDto.DateOfBirth;
            existingProfile.JobTitle = profileDto.JobTitle;

            await _context.SaveChangesAsync();

            return existingProfile;
        }
    }
}

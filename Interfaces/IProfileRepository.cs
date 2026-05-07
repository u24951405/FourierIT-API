using FourierIT_API.DTOs.Profile;
using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IProfileRepository
    {
        Task<List<Profile>> GetAllProfilesAsync();
        Task<Profile?> GetByProfileIdAsync(int profileId); // FirstOrDefault can be NULL, hence the question mark
        Task<Profile> CreateProfileAsync(Profile profileModel);
        Task<Profile?> UpdateProfileAsync(int profileId, UpdateProfileRequestDto profileDto);
        Task<Profile?> DeleteProfileAsync(int profileId);
    }
}

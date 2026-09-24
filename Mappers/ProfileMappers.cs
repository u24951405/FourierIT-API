using FourierIT_API.DTOs.Profile;
using FourierIT_API.Models;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Mappers
{
    public static class ProfileMappers // extension method to convert a Profile entity to a ProfileDto. This allows for a clean separation between the data model used in the database and the data transfer object used for API responses, which can help to improve maintainability and reduce coupling between different layers of the application.
    {
        public static ProfileDto ToProfileDto(this Profile profileModel)
        {
            // Pick the first role assigned to the user (if any)
            var user = profileModel.User;
            var roleNames = user?.UserRoles?
                .Where(ur => ur?.Role?.Name != null)
                .Select(ur => ur!.Role!.Name!)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList() ?? new List<string>();

            return new ProfileDto
            {
                ProfileId = profileModel.ProfileId,
                JobTitle = profileModel.JobTitle ?? string.Empty,
                FirstName = profileModel.FirstName ?? string.Empty,
                LastName = profileModel.LastName ?? string.Empty,
                // Try to populate username from the loaded navigation; controller will override if necessary
                UserName = profileModel.User?.UserName ?? string.Empty,
                DateOfBirth = profileModel.DateOfBirth,
                PhoneNumber = profileModel.PhoneNumber ?? string.Empty,
                Email = profileModel.User?.Email ?? string.Empty,
                Role = roleNames,

            };
        }

        public static Profile ToProfileFromCreateDto(this CreateProfileRequestDto profileDto)
        {
            return new Profile
            {
                JobTitle = profileDto.JobTitle,
                FirstName = profileDto.FirstName,
                LastName = profileDto.LastName,
                DateOfBirth = profileDto.DateOfBirth,
                PhoneNumber = profileDto.PhoneNumber
            };
        }
    }
}
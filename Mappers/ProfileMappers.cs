using FourierIT_API.DTOs.Profile;
using FourierIT_API.Models;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Mappers
{
    public static class ProfileMappers // extension method to convert a Profile entity to a ProfileDto. This allows for a clean separation between the data model used in the database and the data transfer object used for API responses, which can help to improve maintainability and reduce coupling between different layers of the application.
    {
        public static ProfileDto ToProfileDto(this Profile profileModel)
        {
            return new ProfileDto
            {
                ProfileId = profileModel.ProfileId,
                JobTitle = profileModel.JobTitle,
                FirstName = profileModel.FirstName,
                LastName = profileModel.LastName,
                DateOfBirth = profileModel.DateOfBirth,
                PhoneNumber = profileModel.PhoneNumber,
                Email = profileModel.User.Email
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
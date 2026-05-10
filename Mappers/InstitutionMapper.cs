using FourierIT_API.DTOs.Institution;
using FourierIT_API.Models;

namespace FourierIT_API.Mappers
{
    public static class InstitutionMapper
    {
        public static InstitutionDto ToInstitutionDto(this Institution institutionModel)
        {
            return new InstitutionDto
            {
                InstitutionId = institutionModel.InstitutionId,
                InstitutionName = institutionModel.InstitutionName,
                VerifiedDomain = institutionModel.VerifiedDomain,
                RegNumber = institutionModel.RegNumber,
                TypeId = institutionModel.TypeId,
                InstitutionTypeName = institutionModel.InstitutionType?.InstitutionTypeName ?? string.Empty,
            };
        }

        public static Institution ToInstitutionFromCreatDTO(this CreateInstitutionRequestDto dto)
        {
            return new Institution
            {
                InstitutionName = dto.InstitutionName.Trim(),
                VerifiedDomain = dto.VerifiedDomain.Trim(),
                RegNumber = dto.RegNumber,
                TypeId = dto.TypeId,
            };
        }
    }
}

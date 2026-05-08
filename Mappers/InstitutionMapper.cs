using FourierIT_API.DTOs.Department;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Models;

namespace FourierIT_API.Mappers
{
    public static class InstitutionMapper
    {
        public static InstitutionDto ToInstitutionDto(this Institution institutionModel)
        {
            {
                return new InstitutionDto
                {
                    InstitutionId = institutionModel.InstitutionId,
                    InstitutionName = institutionModel.InstitutionName,
                    VerifiedDomain = institutionModel.VerifiedDomain,
                    RegNumber = institutionModel.RegNumber,
                    TypeId = institutionModel.TypeId,
                    InstitutionType= institutionModel.InstitutionType,

                };

            }
        }

        public static Institution ToInstitutionFromCreatDTO(this CreateInstitutionRequestDto InstitutionDto)
        {
            return new Institution
            {
                InstitutionName = InstitutionDto.InstitutionName,
                VerifiedDomain= InstitutionDto.VerifiedDomain,
                 RegNumber=InstitutionDto.RegNumber,
                 TypeId= InstitutionDto.TypeId,
                 InstitutionType=InstitutionDto.InstitutionType,
                


            };
        }
    }
}


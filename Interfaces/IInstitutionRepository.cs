using FourierIT_API.DTOs.Institution;
using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IInstitutionRepository
    {
        Task<List<Institution>> GetAllAsync();
        Task<Institution?> GetByIdAsync(int InstitutionId); //FirstorDefault can be null
        Task<Institution> CreateAsync(Institution institutionModel);    

        Task<Institution> UpdateAsync(int InstitutionId, UpdateInstitutionRequestDto institutionDto);

        Task<Institution?> DeleteAsync(int InstitutionId);
    }
}

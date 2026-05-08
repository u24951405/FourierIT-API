using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Repositories
{
    public class InstitutionRepository : IInstitutionRepository
    {
        private readonly AppDbContext _context;

        public InstitutionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Institution> CreateAsync(Institution institutionModel)
        {
            await _context.Institutions.AddAsync(institutionModel);
            await _context.SaveChangesAsync();
            return institutionModel;
        }

        public async Task<Institution?> DeleteAsync(int InstitutionId)
        {
            var institutionModel = await _context.Institutions.FirstOrDefaultAsync(x => x.InstitutionId == InstitutionId);
            if (institutionModel == null)
            {
                return null;
            } 


            _context.Institutions.Remove(institutionModel);
            await _context.SaveChangesAsync();
            return institutionModel;
        }

        public  async Task<List<Institution>> GetAllAsync()
        {
             return await _context.Institutions.ToListAsync();
        }

        public async Task<Institution?> GetByIdAsync(int InstitutionId)
        {
            return await _context.Institutions.FindAsync(InstitutionId);
        }

        public async Task<Institution> UpdateAsync(int InstitutionId, UpdateInstitutionRequestDto institutionDto)
        {
            var existingInstitution = await _context.Institutions.FirstOrDefaultAsync(x => x.InstitutionId == InstitutionId);
            if (existingInstitution == null)
            {
                return null;
            }

            existingInstitution.InstitutionName = institutionDto.InstitutionName;
            existingInstitution.VerifiedDomain = institutionDto.VerifiedDomain;
            existingInstitution.RegNumber = institutionDto.RegNumber;
            existingInstitution.TypeId = institutionDto.TypeId;
            existingInstitution.InstitutionType = institutionDto.InstitutionType;

            _context.Institutions.Update(existingInstitution);
            await _context.SaveChangesAsync();

            return existingInstitution;
        }
    }
}

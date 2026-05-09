using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/Institution")]
    [ApiController]
    [Authorize(Roles = "Department Admin")]
    public class InstitutionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IInstitutionRepository _institutionRepo;
        public InstitutionController(AppDbContext context,IInstitutionRepository institutionRepo)
        {
            _institutionRepo= institutionRepo;
            _context =context;

        }

        [HttpGet]

        public async Task<IActionResult> GetAll() 
        { 

            var instituions =await _institutionRepo.GetAllAsync();

            var institutionDto= instituions.Select(i => i.ToInstitutionDto());
            return Ok(instituions);
        
        
        
        }

        [HttpGet("{InstitutionId}")]

        public async Task<IActionResult> GetById(int InstitutionId) 
        {
            var institution =await _institutionRepo.GetByIdAsync(InstitutionId);
            if (institution == null)
            { 
                return NotFound();
            }
            return Ok(institution);
        
        
        }


        [HttpPost]
        public async Task<IActionResult> create([FromBody] CreateInstitutionRequestDto InstitutionDto)
        {
            var institutionModel = InstitutionDto.ToInstitutionFromCreatDTO();
            await _institutionRepo.CreateAsync(institutionModel);
            return CreatedAtAction(nameof(GetById), new { id = institutionModel.InstitutionId }, institutionModel.ToInstitutionDto());
        }

        [HttpPut]
        [Route("{InstitutionId}")]
        public async Task<IActionResult> Update([FromRoute] int InstitutionId, [FromBody] UpdateInstitutionRequestDto UpdateDto)
        {
            var institutionModel = await _institutionRepo.UpdateAsync(InstitutionId, UpdateDto);
            if (institutionModel == null)
            {
                return NotFound();
            }
            
            return Ok(institutionModel.ToInstitutionDto());
        }

        [HttpDelete]
        [Route("{InstitutionId}")]
        public async Task<IActionResult> Delete([FromRoute] int InstitutionId)
        {
            var institutionModel =await _institutionRepo.DeleteAsync(InstitutionId);
            if (institutionModel == null)
            {
                return NotFound();
            }
            
            return NoContent();
        }
    }
}

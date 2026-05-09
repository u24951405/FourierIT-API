using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.DTOs.Institution;
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
        public InstitutionController(AppDbContext context)
        {
            _context=context;

        }

        [HttpGet]

        public async Task<IActionResult> GetAll() 
        { 

            var instituions =await _context.Institutions.ToListAsync();

            var institutionDto= instituions.Select(i => i.ToInstitutionDto());
            return Ok(instituions);
        
        
        
        }

        [HttpGet("{InstitutionId}")]

        public async Task<IActionResult> GetById(int InstitutionId) 
        {
            var institution =await _context.Institutions.FindAsync(InstitutionId);
            if (institution == null)
            { 
            
              return NotFound();
            
            }
            return Ok(institution);
        
        
        }


        [HttpPost]
        public async Task<IActionResult> create([FromBody] CreateInstitutionRequestDto InstitutionDto)
        {
            var InstitutionModel = InstitutionDto.ToInstitutionFromCreatDTO();
            await _context.Institutions.AddAsync(InstitutionModel);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = InstitutionModel.InstitutionId }, InstitutionModel.ToInstitutionDto());
        }

        [HttpPut]
        [Route("{InstitutionId}")]
        public async Task<IActionResult> Update([FromRoute] int InstitutionId, [FromBody] UpdateInstitutionRequestDto UpdateDto)
        {
            var InstitutionModel = await _context.Institutions.FirstOrDefaultAsync(x => x.InstitutionId == InstitutionId);
            if (InstitutionModel == null)
            {
                return NotFound();
            }
            InstitutionModel.InstitutionName = UpdateDto.InstitutionName;
            InstitutionModel.VerifiedDomain = UpdateDto.VerifiedDomain;
            InstitutionModel.RegNumber = UpdateDto.RegNumber;
            InstitutionModel.TypeId = UpdateDto.TypeId;
            InstitutionModel.InstitutionType = UpdateDto.InstitutionType;
            await _context.SaveChangesAsync();
            return Ok(InstitutionModel.ToInstitutionDto());
        }

        [HttpDelete]
        [Route("{InstitutionId}")]
        public async Task<IActionResult> Delete([FromRoute] int InstitutionId)
        {
            var InstitutionModel =await _context.Institutions.FirstOrDefaultAsync(x => x.InstitutionId == InstitutionId);
            if (InstitutionModel == null)
            {
                return NotFound();
            }
            _context.Institutions.Remove(InstitutionModel);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}

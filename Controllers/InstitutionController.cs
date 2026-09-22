using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Mappers;
using FourierIT_API.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstitutionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InstitutionController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var institutions = await _context.Institutions
                .AsNoTracking()
                .Include(i => i.InstitutionType)
                .OrderBy(i => i.InstitutionName)
                .ToListAsync();

            return Ok(institutions.Select(i => i.ToInstitutionDto()));
        }

        /// <summary>Lookup rows for institution type dropdowns.</summary>
        [Authorize]
        [HttpGet("types")]
        public async Task<IActionResult> GetInstitutionTypes()
        {
            var types = await _context.InstitutionTypes
                .AsNoTracking()
                .OrderBy(t => t.InstitutionTypeName)
                .Select(t => new InstitutionTypeOptionDto
                {
                    InstitutionTypeId = t.InstitutionTypeId,
                    InstitutionTypeName = t.InstitutionTypeName
                })
                .ToListAsync();

            return Ok(types);
        }

        [Authorize]
        [HttpGet("{institutionId:int}")]
        public async Task<IActionResult> GetById([FromRoute] int institutionId)
        {
            var institution = await _context.Institutions
                .AsNoTracking()
                .Include(i => i.InstitutionType)
                .FirstOrDefaultAsync(i => i.InstitutionId == institutionId);

            if (institution == null)
                return NotFound();

            return Ok(institution.ToInstitutionDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInstitutionRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await _context.InstitutionTypes.AnyAsync(t => t.InstitutionTypeId == dto.TypeId))
                return BadRequest(new { error = "Invalid institution type." });

            var entity = dto.ToInstitutionFromCreatDTO();
            _context.Institutions.Add(entity);
            await _context.SaveChangesAsync();

            await _context.Entry(entity).Reference(i => i.InstitutionType).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { institutionId = entity.InstitutionId }, entity.ToInstitutionDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{institutionId:int}")]
        public async Task<IActionResult> Update([FromRoute] int institutionId, [FromBody] UpdateInstitutionRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var institution = await _context.Institutions.FirstOrDefaultAsync(i => i.InstitutionId == institutionId);
            if (institution == null)
                return NotFound();

            if (!await _context.InstitutionTypes.AnyAsync(t => t.InstitutionTypeId == dto.TypeId))
                return BadRequest(new { error = "Invalid institution type." });

            institution.InstitutionName = dto.InstitutionName.Trim();
            institution.VerifiedDomain = dto.VerifiedDomain.Trim();
            institution.RegNumber = dto.RegNumber;
            institution.TypeId = dto.TypeId;

            await _context.SaveChangesAsync();

            await _context.Entry(institution).Reference(i => i.InstitutionType).LoadAsync();

            return Ok(institution.ToInstitutionDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{institutionId:int}")]
        public async Task<IActionResult> Delete([FromRoute] int institutionId)
        {
            return await this.SafeDeleteAsync(
                "Institution",
                institutionId.ToString(),
                async () =>
                {
                    var institution = await _context.Institutions
                        .Include(i => i.Branches)
                        .Include(i => i.InstitutionMembers)
                        .Include(i => i.ClientEnlistments)
                        .FirstOrDefaultAsync(i => i.InstitutionId == institutionId);

                    if (institution == null)
                        throw new KeyNotFoundException($"Institution with ID {institutionId} not found");

                    _context.Institutions.Remove(institution);
                    await _context.SaveChangesAsync();
                }
            );
        }
    }
}

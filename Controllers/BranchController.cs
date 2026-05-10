using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BranchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BranchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchListDto
                {
                    BranchId = b.BranchId,
                    BranchName = b.BranchName,
                    City = b.City
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}

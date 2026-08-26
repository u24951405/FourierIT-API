using FourierIT_API.Data;
using FourierIT_API.DTOs;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers;

[ApiController]
[Route("api/system-settings")]
[Authorize(Policy = "SuperAdminOnly")]
public class SystemSettingsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SystemSettingsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SystemSettingDto>>> GetAll()
    {
        return Ok(await _context.SystemSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Key)
            .Select(setting => new SystemSettingDto
            {
                Key = setting.Key,
                Value = setting.Value,
                Description = setting.Description
            })
            .ToListAsync());
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<SystemSettingDto>> Update(string key, UpdateSystemSettingRequest request)
    {
        if (!int.TryParse(request.Value, out var value) || value < 1 || value > 1440)
            return BadRequest(new { error = "The setting must be a whole number between 1 and 1440." });

        var setting = await _context.SystemSettings.FindAsync(key);
        if (setting == null)
            return NotFound(new { error = "System setting not found." });

        setting.Value = value.ToString();
        await _context.SaveChangesAsync();

        return Ok(new SystemSettingDto
        {
            Key = setting.Key,
            Value = setting.Value,
            Description = setting.Description
        });
    }
}

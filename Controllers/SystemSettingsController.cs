using FourierIT_API.Data;
using FourierIT_API.DTOs;
using FourierIT_API.Models;
using FourierIT_API.Services;
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

    /// <summary>All configurable timers and limits, in display order, with their current values.</summary>
    [HttpGet]
    public async Task<ActionResult<List<SystemSettingDto>>> GetAll()
    {
        var stored = await _context.SystemSettings
            .AsNoTracking()
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, StringComparer.OrdinalIgnoreCase);

        return Ok(SystemSettingDefinitions.All
            .Select(definition => ToDto(definition, stored.GetValueOrDefault(definition.Key)))
            .ToList());
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<SystemSettingDto>> Update(string key, UpdateSystemSettingRequest request)
    {
        var definition = SystemSettingDefinitions.Find(key);
        if (definition == null)
            return NotFound(new { error = "System setting not found." });

        if (!int.TryParse(request.Value, out var value) || value < definition.Min || value > definition.Max)
        {
            return BadRequest(new
            {
                error = $"{definition.Title} must be a whole number from {definition.Min} to {definition.Max} {UnitLabel(definition.Unit)}."
            });
        }

        var setting = await _context.SystemSettings.FindAsync(definition.Key);
        if (setting == null)
        {
            setting = new SystemSetting { Key = definition.Key, Description = definition.Description };
            _context.SystemSettings.Add(setting);
        }

        setting.Value = value.ToString();
        await _context.SaveChangesAsync();

        return Ok(ToDto(definition, setting.Value));
    }

    private static SystemSettingDto ToDto(SystemSettingDefinition definition, string? storedValue)
    {
        var value = int.TryParse(storedValue, out var parsed)
            ? Math.Clamp(parsed, definition.Min, definition.Max)
            : definition.Default;

        return new SystemSettingDto
        {
            Key = definition.Key,
            Value = value.ToString(),
            Description = definition.Description,
            Category = definition.Category,
            Title = definition.Title,
            Unit = definition.Unit.ToString().ToLowerInvariant(),
            Min = definition.Min,
            Max = definition.Max,
            Default = definition.Default
        };
    }

    private static string UnitLabel(SettingUnit unit) => unit == SettingUnit.Count ? "attempts" : unit.ToString().ToLowerInvariant();
}

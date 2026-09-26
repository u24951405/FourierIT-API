using FourierIT_API.Data;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Services;

public interface ISystemSettingsService
{
    /// <summary>
    /// The current value of a setting from <see cref="SystemSettingDefinitions"/>. Falls back to the default
    /// when the value is missing or invalid, and is always kept within the setting's allowed range.
    /// </summary>
    Task<int> GetAsync(string key);
}

public sealed class SystemSettingsService : ISystemSettingsService
{
    private readonly AppDbContext _context;

    public SystemSettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetAsync(string key)
    {
        var definition = SystemSettingDefinitions.Find(key)
            ?? throw new ArgumentException($"Unknown system setting '{key}'.", nameof(key));

        var stored = await _context.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == definition.Key)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync();

        return int.TryParse(stored, out var value)
            ? Math.Clamp(value, definition.Min, definition.Max)
            : definition.Default;
    }
}

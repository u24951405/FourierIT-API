namespace FourierIT_API.DTOs;

public sealed class SystemSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // How the System Settings page should present and validate the setting.
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Min { get; set; }
    public int Max { get; set; }
    public int Default { get; set; }
}

public sealed class UpdateSystemSettingRequest
{
    public string Value { get; set; } = string.Empty;
}

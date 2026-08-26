namespace FourierIT_API.DTOs;

public sealed class SystemSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class UpdateSystemSettingRequest
{
    public string Value { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models;

public class SystemSetting
{
    [Key]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Value { get; set; } = string.Empty;

    [StringLength(250)]
    public string Description { get; set; } = string.Empty;
}

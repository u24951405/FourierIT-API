using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models;

public class PendingRegistration
{
    [Key]
    public int PendingRegistrationId { get; set; }

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string NormalizedUserName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string JobTitle { get; set; } = string.Empty;

    public int? EntityTypeId { get; set; }

    [MaxLength(100)]
    public string EntityIdentificationNumber { get; set; } = string.Empty;

    [Required]
    public string RequestedRolesJson { get; set; } = "[]";

    [Required]
    public string OtpHash { get; set; } = string.Empty;

    public DateTimeOffset OtpExpiry { get; set; }

    // Wrong codes entered for the current code; reset whenever a new code is sent.
    public int FailedAttempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
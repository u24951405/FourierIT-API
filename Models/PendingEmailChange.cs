using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models;

/// <summary>
/// An email change waiting for the user to confirm the one-time code sent to the new address.
/// A user has at most one pending change; requesting a new one replaces it.
/// </summary>
public class PendingEmailChange
{
    [Key]
    public int PendingEmailChangeId { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    [Required, MaxLength(256)]
    public string NewEmail { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string NormalizedNewEmail { get; set; } = string.Empty;

    [Required]
    public string OtpHash { get; set; } = string.Empty;

    public DateTimeOffset OtpExpiry { get; set; }

    public int FailedAttempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

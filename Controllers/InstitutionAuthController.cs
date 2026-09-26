using System.Security.Cryptography;
using System.Text;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers
{
    [Route("api/institution/auth")]
    [ApiController]
    public class InstitutionAuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InstitutionAuthController> _logger;
        private readonly IEmailService _emailService;
        private readonly ISystemSettingsService _settings;

        public InstitutionAuthController(
            AppDbContext context,
            ILogger<InstitutionAuthController> logger,
            IEmailService emailService,
            ISystemSettingsService? settings = null)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _settings = settings ?? new SystemSettingsService(context);
        }

        [Authorize]
        [HttpPost("invite")]
        public async Task<IActionResult> Invite([FromBody] InviteInstitutionRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!User.HasClaim("superadmin", "true"))
                return Forbid();

            var institution = await _context.Institutions.FindAsync(dto.InstitutionId);
            if (institution == null)
                return NotFound(new { error = "Institution not found." });

            // Prior invitations stay usable until they expire or are revoked.
            var tokenString = GenerateTokenString();
            var now = DateTimeOffset.UtcNow;
            var invitation = new InstitutionInvitation
            {
                InstitutionId = dto.InstitutionId,
                Email = dto.Email.Trim(),
                TokenString = tokenString,
                OtpCodeHash = string.Empty,
                TokenExpiryTimeStamp = now.AddDays(await _settings.GetAsync(SystemSettingDefinitions.InstitutionInvitationExpiryDays)),
                OtpExpiryTimeStamp = now,
                IsRevoked = false,
                IsUsed = false,
                OtpSendCount = 0,
                CreatedAt = now
            };

            _context.InstitutionInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            var supportedToken = tokenString;
            var accessLink = BuildAccessLink(supportedToken);

            try
            {
                await _emailService.SendInvitationEmailAsync(invitation.Email, institution.InstitutionName, accessLink, invitation.TokenExpiryTimeStamp);
                // Never log the access token: anyone who can read the logs could use the link.
                _logger.LogInformation("Institution invitation email sent for institution {InstitutionId}. Email={Email}",
                    dto.InstitutionId, invitation.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invitation email for institution {InstitutionId} to {Email}.", dto.InstitutionId, invitation.Email);
                return StatusCode(500, new { error = "Invitation created but email delivery failed. Please verify SMTP configuration and recipient email." });
            }

            return Ok(new InviteInstitutionResponseDto
            {
                AccessToken = supportedToken,
                ExpiresAt = invitation.TokenExpiryTimeStamp,
                MaskedEmail = MaskEmail(invitation.Email)
            });
        }

        [AllowAnonymous]
        [HttpPost("validate-token")]
        public async Task<IActionResult> ValidateToken([FromBody] TokenValidationRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var invitation = await _context.InstitutionInvitations
                .Include(ii => ii.Institution)
                .FirstOrDefaultAsync(ii => ii.TokenString == dto.AccessToken);

            if (invitation == null)
            {
                return Ok(new TokenValidationResponseDto { Valid = false });
            }

            if (invitation.IsUsed)
            {
                return BadRequest(new { error = "This invitation has already been used.", reason = "used", institutionId = invitation.InstitutionId });
            }

            var unusable = await CheckInvitationUsableAsync(invitation);
            if (unusable != null) return unusable;

            // Reopening the link shortly after a code was sent reuses that code instead of emailing another.
            if (await ResendWaitAsync(invitation) > TimeSpan.Zero && invitation.OtpExpiryTimeStamp > DateTimeOffset.UtcNow)
            {
                return Ok(ValidationResponse(invitation));
            }

            var otpCode = await IssueOtpAsync(invitation);

            try
            {
                await _emailService.SendInstitutionOtpEmailAsync(invitation.Email, invitation.Institution.InstitutionName, otpCode, invitation.OtpExpiryTimeStamp);
                // Never log the code itself: anyone who can read the logs could use it to sign in.
                _logger.LogInformation("OTP sent for institution invitation {InvitationId}. Email={Email}",
                    invitation.InvitationId, invitation.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email for institution invitation {InvitationId} to {Email}.", invitation.InvitationId, invitation.Email);
                return StatusCode(500, new { error = "OTP generated but delivery failed. Please try again or contact support." });
            }

            return Ok(ValidationResponse(invitation));
        }

        private static TokenValidationResponseDto ValidationResponse(InstitutionInvitation invitation) => new()
        {
            Valid = true,
            InstitutionId = invitation.InstitutionId.ToString(),
            InstitutionName = invitation.Institution.InstitutionName,
            InstitutionCode = invitation.Institution.VerifiedDomain,
            MaskedEmail = MaskEmail(invitation.Email)
        };

        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(dto.Otp))
                return BadRequest(new { error = "OTP is required." });

            if (string.IsNullOrWhiteSpace(dto.AccessToken))
                return BadRequest(new { error = "Access token is required." });

            if (!int.TryParse(dto.InstitutionId, out var institutionId))
                return BadRequest(new { error = "Invalid institution identifier." });

            var invitation = await _context.InstitutionInvitations
                    .FirstOrDefaultAsync(ii => ii.TokenString == dto.AccessToken && ii.InstitutionId == institutionId);

                if (invitation == null)
                {
                    return BadRequest(new { error = "Invalid invitation link." });
                }

                if (invitation.IsUsed)
                {
                    return BadRequest(new { error = "This invitation has already been used.", reason = "used", institutionId = invitation.InstitutionId });
                }

                var unusable = await CheckInvitationUsableAsync(invitation);
                if (unusable != null) return unusable;

                if (string.IsNullOrWhiteSpace(invitation.OtpCodeHash) || invitation.OtpExpiryTimeStamp <= DateTimeOffset.UtcNow)
                {
                    return BadRequest(new { error = "This code has expired. Request a new code." });
                }

                if (!VerifyHash(dto.Otp.Trim(), invitation.OtpCodeHash))
                {
                    invitation.OtpFailedAttempts++;
                    var attemptsRemaining = await _settings.GetAsync(SystemSettingDefinitions.MaxCodeAttempts) - invitation.OtpFailedAttempts;
                    if (attemptsRemaining <= 0)
                    {
                        // Too many wrong guesses: cancel this code so it cannot be brute-forced.
                        invitation.OtpCodeHash = string.Empty;
                        invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogWarning("Institution code cancelled after too many incorrect attempts for invitation {InvitationId}.", invitation.InvitationId);
                        return BadRequest(new { error = "Too many incorrect codes. Request a new code to try again.", attemptsRemaining = 0 });
                    }

                    await _context.SaveChangesAsync();
                    return BadRequest(new
                    {
                        error = $"Incorrect code. {attemptsRemaining} attempt{(attemptsRemaining == 1 ? "" : "s")} left.",
                        attemptsRemaining
                    });
                }

                invitation.IsUsed = true;
                invitation.OtpCodeHash = string.Empty;
                invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow;

                var sessionToken = GenerateSessionToken();
                var expiresAt = DateTimeOffset.UtcNow.AddMinutes(await _settings.GetAsync(SystemSettingDefinitions.InstitutionSessionTimeoutMinutes));

                // Store session token in database
                var sessionRecord = new InstitutionSessionToken
                {
                    InstitutionId = invitation.InstitutionId,
                    TokenString = sessionToken,
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt.DateTime,
                    IsRevoked = false
                };
                _context.InstitutionSessionTokens.Add(sessionRecord);
                await _context.SaveChangesAsync();

                var response = new OtpVerifyResponseDto
                {
                    Success = true,
                    SessionToken = sessionToken,
                    ExpiresAt = expiresAt,
                    Message = "OTP verified successfully."
                };

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] OtpResendRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!int.TryParse(dto.InstitutionId, out var institutionId))
                return BadRequest(new { error = "Invalid institution identifier." });

            var invitation = await _context.InstitutionInvitations
                .FirstOrDefaultAsync(ii => ii.TokenString == dto.AccessToken && ii.InstitutionId == institutionId);

            if (invitation == null)
            {
                return BadRequest(new { error = "Invalid invitation link." });
            }

            if (invitation.IsUsed)
            {
                return BadRequest(new { error = "This invitation has already been used.", reason = "used", institutionId = invitation.InstitutionId });
            }

            var unusable = await CheckInvitationUsableAsync(invitation);
            if (unusable != null) return unusable;

            var wait = await ResendWaitAsync(invitation);
            if (wait > TimeSpan.Zero)
                return TooSoon(wait, "requesting another code");

            var otpCode = await IssueOtpAsync(invitation);

            try
            {
                await _emailService.SendInstitutionOtpEmailAsync(invitation.Email, invitation.Institution?.InstitutionName ?? string.Empty, otpCode, invitation.OtpExpiryTimeStamp);
                // Never log the code itself: anyone who can read the logs could use it to sign in.
                _logger.LogInformation("OTP resent for institution invitation {InvitationId}. Email={Email}",
                    invitation.InvitationId, invitation.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend OTP email for institution invitation {InvitationId} to {Email}.", invitation.InvitationId, invitation.Email);
                return StatusCode(500, new { error = "OTP resent but delivery failed. Please try again or contact support." });
            }

            return Ok();
        }

        [AllowAnonymous]
        [HttpPost("request-new-token")]
        public async Task<IActionResult> RequestNewToken([FromBody] RequestNewTokenRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!int.TryParse(dto.InstitutionId, out var institutionId))
                return BadRequest(new { error = "Invalid institution identifier." });

            var existingInvitation = await _context.InstitutionInvitations
                .Include(ii => ii.Institution)
                .Where(ii => ii.InstitutionId == institutionId)
                .OrderByDescending(ii => ii.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingInvitation == null)
                return BadRequest(new { error = "No active invitation found for this institution." });

            // Anyone can call this, so limit how often a new link can be emailed.
            var cooldown = TimeSpan.FromSeconds(await _settings.GetAsync(SystemSettingDefinitions.CodeResendCooldownSeconds));
            var linkWait = cooldown - (DateTimeOffset.UtcNow - existingInvitation.CreatedAt);
            if (linkWait > TimeSpan.Zero)
                return TooSoon(linkWait, "requesting another link");

            // Earlier links stay usable until they expire or are revoked.
            var tokenString = GenerateTokenString();
            var now = DateTimeOffset.UtcNow;
            var newInvitation = new InstitutionInvitation
            {
                InstitutionId = existingInvitation.InstitutionId,
                Email = existingInvitation.Email,
                TokenString = tokenString,
                OtpCodeHash = string.Empty,
                TokenExpiryTimeStamp = now.AddDays(await _settings.GetAsync(SystemSettingDefinitions.InstitutionInvitationExpiryDays)),
                OtpExpiryTimeStamp = now,
                IsRevoked = false,
                IsUsed = false,
                OtpSendCount = 0,
                CreatedAt = now
            };

            _context.InstitutionInvitations.Add(newInvitation);
            await _context.SaveChangesAsync();

            var accessLink = BuildAccessLink(tokenString);
            try
            {
                await _emailService.SendInvitationEmailAsync(newInvitation.Email, existingInvitation.Institution?.InstitutionName ?? string.Empty, accessLink, newInvitation.TokenExpiryTimeStamp);
                // Never log the access token: anyone who can read the logs could use the link.
                _logger.LogInformation("New institution invitation email sent for institution {InstitutionId}. Email={Email}",
                    existingInvitation.InstitutionId, newInvitation.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new institution invitation email for institution {InstitutionId} to {Email}.", existingInvitation.InstitutionId, newInvitation.Email);
                return StatusCode(500, new { error = "New invitation created but email delivery failed. Please verify SMTP configuration and recipient email." });
            }

            return Ok(new { AccessToken = tokenString });
        }

        /// <summary>Refuses revoked or expired invitation links; returns null when the link can be used.</summary>
        private async Task<IActionResult?> CheckInvitationUsableAsync(InstitutionInvitation invitation)
        {
            if (invitation.IsRevoked)
                return BadRequest(new { error = "This invitation link has been revoked. Request a new link.", reason = "revoked" });

            // Links created before expiry was introduced were stored without one; they expire the same way,
            // counted from when they were created.
            var expiresAt = invitation.TokenExpiryTimeStamp == DateTimeOffset.MaxValue
                ? invitation.CreatedAt.AddDays(await _settings.GetAsync(SystemSettingDefinitions.InstitutionInvitationExpiryDays))
                : invitation.TokenExpiryTimeStamp;

            return expiresAt <= DateTimeOffset.UtcNow
                ? BadRequest(new { error = "This invitation link has expired. Request a new link.", reason = "expired", institutionId = invitation.InstitutionId })
                : null;
        }

        /// <summary>How long until another code may be emailed for this invitation (zero when allowed now).</summary>
        private async Task<TimeSpan> ResendWaitAsync(InstitutionInvitation invitation)
        {
            if (invitation.OtpLastSentAt == null) return TimeSpan.Zero;
            var cooldown = TimeSpan.FromSeconds(await _settings.GetAsync(SystemSettingDefinitions.CodeResendCooldownSeconds));
            var wait = cooldown - (DateTimeOffset.UtcNow - invitation.OtpLastSentAt.Value);
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        /// <summary>Creates a fresh code for the invitation, resetting the wrong-attempt count.</summary>
        private async Task<string> IssueOtpAsync(InstitutionInvitation invitation)
        {
            var otpCode = GenerateOtpCode();
            var now = DateTimeOffset.UtcNow;
            invitation.OtpCodeHash = HashValue(otpCode);
            invitation.OtpExpiryTimeStamp = now.AddMinutes(await _settings.GetAsync(SystemSettingDefinitions.InstitutionOtpExpiryMinutes));
            invitation.OtpFailedAttempts = 0;
            invitation.OtpLastSentAt = now;
            invitation.OtpSendCount += 1;
            await _context.SaveChangesAsync();
            return otpCode;
        }

        private ObjectResult TooSoon(TimeSpan wait, string action)
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = $"Please wait {seconds} seconds before {action}.",
                retryAfterSeconds = seconds
            });
        }

        private static string GenerateTokenString()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string BuildAccessLink(string token)
        {
            var origin = Request?.Headers["Origin"].ToString();
            if (string.IsNullOrWhiteSpace(origin))
            {
                origin = Url.ActionContext.HttpContext.Request.Scheme + "://" + Url.ActionContext.HttpContext.Request.Host;
            }

            return $"{origin}/institution/auth/access?token={token}";
        }

        private static string GenerateSessionToken()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string GenerateOtpCode()
        {
            var rng = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return rng.ToString("D6");
        }

        private static string HashValue(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyHash(string value, string hash)
        {
            return HashValue(value) == hash;
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            var at = email.IndexOf('@');
            if (at <= 1)
                return email;

            return email[0] + "*****" + email.Substring(at - 1);
        }
    }
}

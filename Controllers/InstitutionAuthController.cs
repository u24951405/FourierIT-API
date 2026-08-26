using System.Security.Cryptography;
using System.Text;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
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

        public InstitutionAuthController(AppDbContext context, ILogger<InstitutionAuthController> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
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

            // Do not revoke prior invitations. Access links are permanent and any previously issued token
            // should remain valid until manually revoked or a new token is explicitly requested.

            var tokenString = GenerateTokenString();
            var now = DateTimeOffset.UtcNow;
            var invitation = new InstitutionInvitation
            {
                InstitutionId = dto.InstitutionId,
                Email = dto.Email.Trim(),
                TokenString = tokenString,
                OtpCodeHash = string.Empty,
                TokenExpiryTimeStamp = DateTimeOffset.MaxValue,
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
                _logger.LogInformation("Institution invitation email sent for institution {InstitutionId}. Email={Email}, token={Token}",
                    dto.InstitutionId, invitation.Email, supportedToken);
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
                return BadRequest(new { error = "This invitation has already been used." });
            }

            var otpCode = GenerateOtpCode();
            invitation.OtpCodeHash = HashValue(otpCode);
            invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow.AddMinutes(await GetSettingMinutesAsync("InstitutionOtpExpiryMinutes", 10));
            invitation.OtpSendCount += 1;
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendInstitutionOtpEmailAsync(invitation.Email, invitation.Institution.InstitutionName, otpCode, invitation.OtpExpiryTimeStamp);
                _logger.LogInformation("OTP sent for institution invitation {InvitationId}. Email={Email}, otp={Otp}",
                    invitation.InvitationId, invitation.Email, otpCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email for institution invitation {InvitationId} to {Email}.", invitation.InvitationId, invitation.Email);
                return StatusCode(500, new { error = "OTP generated but delivery failed. Please try again or contact support." });
            }

            return Ok(new TokenValidationResponseDto
            {
                Valid = true,
                InstitutionId = invitation.InstitutionId.ToString(),
                InstitutionName = invitation.Institution.InstitutionName,
                InstitutionCode = invitation.Institution.VerifiedDomain,
                MaskedEmail = MaskEmail(invitation.Email)
            });
        }

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
                    return BadRequest(new { error = "This invitation has already been used." });
                }

                if (invitation.OtpExpiryTimeStamp <= DateTimeOffset.UtcNow)
                {
                    return BadRequest(new { error = "OTP expired. Please request a new code." });
                }

                if (string.IsNullOrWhiteSpace(invitation.OtpCodeHash) || !VerifyHash(dto.Otp.Trim(), invitation.OtpCodeHash))
                {
                    return BadRequest(new { error = "Invalid OTP." });
                }

                invitation.IsUsed = true;
                invitation.OtpCodeHash = string.Empty;
                invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow;

                var sessionToken = GenerateSessionToken();
                var expiresAt = DateTimeOffset.UtcNow.AddMinutes(await GetSettingMinutesAsync("InstitutionSessionTimeoutMinutes", 480));

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
                return BadRequest(new { error = "This invitation has already been used." });
            }

            var otpCode = GenerateOtpCode();
            invitation.OtpCodeHash = HashValue(otpCode);
            invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow.AddMinutes(await GetSettingMinutesAsync("InstitutionOtpExpiryMinutes", 10));
            invitation.OtpSendCount += 1;
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendInstitutionOtpEmailAsync(invitation.Email, invitation.Institution?.InstitutionName ?? string.Empty, otpCode, invitation.OtpExpiryTimeStamp);
                _logger.LogInformation("OTP resent for institution invitation {InvitationId}. Email={Email}, otp={Otp}",
                    invitation.InvitationId, invitation.Email, otpCode);
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

            // Keep existing invitations valid. New tokens are additional permanent invite links.
            var tokenString = GenerateTokenString();
            var now = DateTimeOffset.UtcNow;
            var newInvitation = new InstitutionInvitation
            {
                InstitutionId = existingInvitation.InstitutionId,
                Email = existingInvitation.Email,
                TokenString = tokenString,
                OtpCodeHash = string.Empty,
                TokenExpiryTimeStamp = DateTimeOffset.MaxValue,
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
                _logger.LogInformation("New institution invitation email sent for institution {InstitutionId}. Email={Email}, token={Token}",
                    existingInvitation.InstitutionId, newInvitation.Email, tokenString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new institution invitation email for institution {InstitutionId} to {Email}.", existingInvitation.InstitutionId, newInvitation.Email);
                return StatusCode(500, new { error = "New invitation created but email delivery failed. Please verify SMTP configuration and recipient email." });
            }

            return Ok(new { AccessToken = tokenString });
        }

        private async Task<int> GetSettingMinutesAsync(string key, int fallback)
        {
            var setting = await _context.SystemSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Key == key);

            return int.TryParse(setting?.Value, out var minutes) && minutes > 0
                ? minutes
                : fallback;
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

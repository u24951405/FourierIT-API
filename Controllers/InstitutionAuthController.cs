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

            var existingInvitation = await _context.InstitutionInvitations
                .Where(ii => ii.InstitutionId == dto.InstitutionId && !ii.IsRevoked)
                .OrderByDescending(ii => ii.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingInvitation != null)
            {
                existingInvitation.IsRevoked = true;
            }

            var tokenString = GenerateTokenString();
            var otpCode = GenerateOtpCode();
            var now = DateTimeOffset.UtcNow;
            var invitation = new InstitutionInvitation
            {
                InstitutionId = dto.InstitutionId,
                Email = dto.Email.Trim(),
                TokenString = tokenString,
                OtpCodeHash = HashValue(otpCode),
                TokenExpiryTimeStamp = now.AddHours(48),
                OtpExpiryTimeStamp = now.AddMinutes(10),
                IsRevoked = false,
                IsUsed = false,
                OtpSendCount = 1,
                CreatedAt = now
            };

            _context.InstitutionInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            var supportedToken = tokenString;
            var accessLink = BuildAccessLink(supportedToken);

            try
            {
                await _emailService.SendInvitationEmailAsync(invitation.Email, institution.InstitutionName, accessLink, invitation.TokenExpiryTimeStamp);
                _logger.LogInformation("Institution invitation email sent for institution {InstitutionId}. Email={Email}, token={Token}, otp={Otp}",
                    dto.InstitutionId, invitation.Email, supportedToken, otpCode);
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

            if (invitation == null || invitation.IsRevoked || invitation.IsUsed || invitation.TokenExpiryTimeStamp <= DateTimeOffset.UtcNow)
            {
                if (invitation != null && !invitation.IsRevoked && invitation.TokenExpiryTimeStamp <= DateTimeOffset.UtcNow)
                {
                    invitation.IsRevoked = true;
                    await _context.SaveChangesAsync();
                }

                return Ok(new TokenValidationResponseDto { Valid = false });
            }

            var otpCode = GenerateOtpCode();
            invitation.OtpCodeHash = HashValue(otpCode);
            invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow.AddMinutes(10);
            invitation.OtpSendCount += 1;
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendOtpEmailAsync(invitation.Email, invitation.Institution.InstitutionName, otpCode, invitation.OtpExpiryTimeStamp);
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

            if (!int.TryParse(dto.InstitutionId, out var institutionId))
                return BadRequest(new { error = "Invalid institution identifier." });

            var invitation = await _context.InstitutionInvitations
                .FirstOrDefaultAsync(ii => ii.TokenString == dto.AccessToken && ii.InstitutionId == institutionId);

            if (invitation == null || invitation.IsRevoked || invitation.IsUsed || invitation.TokenExpiryTimeStamp <= DateTimeOffset.UtcNow)
            {
                return BadRequest(new { error = "Invalid or expired invitation link." });
            }

            if (invitation.OtpExpiryTimeStamp <= DateTimeOffset.UtcNow)
            {
                return BadRequest(new { error = "OTP expired. Please request a new code." });
            }

            if (!VerifyHash(dto.Otp.Trim(), invitation.OtpCodeHash))
            {
                return BadRequest(new { error = "Invalid OTP." });
            }

            invitation.IsUsed = true;
            invitation.IsRevoked = true;

            var sessionToken = GenerateSessionToken();
            var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

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

            if (invitation == null || invitation.IsRevoked || invitation.IsUsed || invitation.TokenExpiryTimeStamp <= DateTimeOffset.UtcNow)
            {
                return BadRequest(new { error = "Invalid or expired invitation link." });
            }

            var otpCode = GenerateOtpCode();
            invitation.OtpCodeHash = HashValue(otpCode);
            invitation.OtpExpiryTimeStamp = DateTimeOffset.UtcNow.AddMinutes(10);
            invitation.OtpSendCount += 1;
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendOtpEmailAsync(invitation.Email, invitation.Institution?.InstitutionName ?? string.Empty, otpCode, invitation.OtpExpiryTimeStamp);
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
                .Where(ii => ii.InstitutionId == institutionId && !ii.IsRevoked)
                .OrderByDescending(ii => ii.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingInvitation == null)
                return BadRequest(new { error = "No active invitation found for this institution." });

            existingInvitation.IsRevoked = true;

            var tokenString = GenerateTokenString();
            var otpCode = GenerateOtpCode();
            var now = DateTimeOffset.UtcNow;
            var newInvitation = new InstitutionInvitation
            {
                InstitutionId = existingInvitation.InstitutionId,
                Email = existingInvitation.Email,
                TokenString = tokenString,
                OtpCodeHash = HashValue(otpCode),
                TokenExpiryTimeStamp = now.AddHours(48),
                OtpExpiryTimeStamp = now.AddMinutes(10),
                IsRevoked = false,
                IsUsed = false,
                OtpSendCount = 1,
                CreatedAt = now
            };

            _context.InstitutionInvitations.Add(newInvitation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New institution invitation token created for institution {InstitutionId}. Email={Email}, token={Token}, otp={Otp}",
                existingInvitation.InstitutionId, existingInvitation.Email, tokenString, otpCode);

            return Ok(new { AccessToken = tokenString });
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

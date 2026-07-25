using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.Institution
{
    public class InviteInstitutionRequestDto
    {
        [Required]
        public int InstitutionId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class InviteInstitutionResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string MaskedEmail { get; set; } = string.Empty;
    }

    public class TokenValidationRequestDto
    {
        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class TokenValidationResponseDto
    {
        public bool Valid { get; set; }
        public string InstitutionId { get; set; } = string.Empty;
        public string InstitutionName { get; set; } = string.Empty;
        public string InstitutionCode { get; set; } = string.Empty;
        public string MaskedEmail { get; set; } = string.Empty;
    }

    public class OtpVerifyRequestDto
    {
        [Required]
        public string InstitutionId { get; set; } = string.Empty;

        [Required]
        public string Otp { get; set; } = string.Empty;

        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class OtpVerifyResponseDto
    {
        public bool Success { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string? Message { get; set; }
    }

    public class OtpResendRequestDto
    {
        [Required]
        public string InstitutionId { get; set; } = string.Empty;

        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class RequestNewTokenRequestDto
    {
        [Required]
        public string InstitutionId { get; set; } = string.Empty;
    }
}

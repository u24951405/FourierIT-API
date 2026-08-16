using System.Threading.Tasks;

namespace FourierIT_API.Interfaces
{
    public interface IEmailService
    {
        Task SendInvitationEmailAsync(string toEmail, string institutionName, string accessLink, DateTimeOffset expiresAt);
        Task SendUserRegistrationOtpEmailAsync(string toEmail, string otpCode, DateTimeOffset expiresAt);
        Task SendInstitutionOtpEmailAsync(string toEmail, string institutionName, string otpCode, DateTimeOffset expiresAt);
        Task SendOtpEmailAsync(string toEmail, string institutionName, string otpCode, DateTimeOffset expiresAt);
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink, DateTimeOffset expiresAt);
    }
}

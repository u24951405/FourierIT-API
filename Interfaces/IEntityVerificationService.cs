using System.Threading.Tasks;

namespace FourierIT_API.Interfaces
{
    public interface IEntityVerificationService
    {
        Task<EntityVerificationResult> VerifyEntityAsync(int entityTypeId, string identificationNumber);
    }

    public sealed record EntityVerificationResult(bool IsValid, bool HasProviderConfigured, bool ProviderUnavailable, string? ErrorMessage)
    {
        public static EntityVerificationResult Valid(bool hasProviderConfigured = false, bool providerUnavailable = false) => new(true, hasProviderConfigured, providerUnavailable, null);
        public static EntityVerificationResult Invalid(string message, bool hasProviderConfigured = true, bool providerUnavailable = false) => new(false, hasProviderConfigured, providerUnavailable, message);
    }
}

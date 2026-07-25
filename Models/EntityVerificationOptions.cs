namespace FourierIT_API.Models
{
    public class EntityVerificationOptions
    {
        public bool EnableExternalVerification { get; set; } = false;
        public bool UseFallbackOnExternalFailure { get; set; } = true;
        public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
        public string ApiKey { get; set; } = string.Empty;
        public string SouthAfricanIdApiUrl { get; set; } = string.Empty;
        public string PassportApiUrl { get; set; } = string.Empty;
        public string CompanyRegistrationApiUrl { get; set; } = string.Empty;
        public string TrustRegistrationApiUrl { get; set; } = string.Empty;
        public string PartnershipRegistrationApiUrl { get; set; } = string.Empty;
        public string OtherEntityApiUrl { get; set; } = string.Empty;
    }
}

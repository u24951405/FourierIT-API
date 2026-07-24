using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace FourierIT_API.Services
{
    public class FallbackEntityVerificationService : IEntityVerificationService
    {
        private readonly ExternalEntityVerificationService _externalService;
        private readonly LocalEntityVerificationService _localService;
        private readonly EntityVerificationOptions _options;

        public FallbackEntityVerificationService(
            ExternalEntityVerificationService externalService,
            LocalEntityVerificationService localService,
            IOptions<EntityVerificationOptions> options)
        {
            _externalService = externalService;
            _localService = localService;
            _options = options.Value;
        }

        public async Task<EntityVerificationResult> VerifyEntityAsync(int entityTypeId, string identificationNumber)
        {
            if (!_options.EnableExternalVerification)
            {
                return await _localService.VerifyEntityAsync(entityTypeId, identificationNumber);
            }

            var externalResult = await _externalService.VerifyEntityAsync(entityTypeId, identificationNumber);
            if (externalResult.IsValid)
            {
                return EntityVerificationResult.Valid(true);
            }

            if (_options.UseFallbackOnExternalFailure && externalResult.ProviderUnavailable)
            {
                var localResult = await _localService.VerifyEntityAsync(entityTypeId, identificationNumber);
                if (localResult.IsValid)
                {
                    return EntityVerificationResult.Valid(true, true);
                }
                return EntityVerificationResult.Invalid(localResult.ErrorMessage ?? "Local fallback verification failed.", true, true);
            }

            return externalResult;
        }
    }
}

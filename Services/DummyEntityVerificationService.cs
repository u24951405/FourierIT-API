using FourierIT_API.Interfaces;
using System.Threading.Tasks;

namespace FourierIT_API.Services
{
    public class DummyEntityVerificationService : IEntityVerificationService
    {
        public Task<EntityVerificationResult> VerifyEntityAsync(int entityTypeId, string identificationNumber)
        {
            // Placeholder implementation to demonstrate external verification integration.
            // In a real deployment, this should call the configured external verification provider.
            if (string.IsNullOrWhiteSpace(identificationNumber))
            {
                return Task.FromResult(EntityVerificationResult.Invalid("Identification number is required.", true));
            }

            // For SA ID we can validate the checksum locally as an example
            if (entityTypeId == 1)
            {
                if (!IsValidSouthAfricanId(identificationNumber))
                {
                    return Task.FromResult(EntityVerificationResult.Invalid("The South African ID number is not valid.", true));
                }
            }

            return Task.FromResult(EntityVerificationResult.Valid(true));
        }

        private static bool IsValidSouthAfricanId(string idNumber)
        {
            if (idNumber.Length != 13 || !idNumber.All(char.IsDigit))
                return false;

            // Validate date portion
            if (!DateTime.TryParseExact(idNumber.Substring(0, 6), "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var birthDate))
                return false;

            // Checksum validation using the Luhn algorithm variant for SA ID
            var digits = idNumber.Select(c => c - '0').ToArray();
            var sum = digits[0];
            for (var i = 1; i < 12; i += 2)
            {
                var doubled = digits[i] * 2;
                sum += doubled / 10 + doubled % 10;
                sum += digits[i + 1];
            }
            var checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == digits[12];
        }
    }
}
